using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class Bg : MonoBehaviour
{
    private const int indexEmpty = -1;
    private const int indexMiner = 0;
    private const int indexFlag = 13;
    private const int indexQues = 14;

    private const int statusReady = 1;
    private const int statusRun = 2;
    private const int statusWin = 3;
    private const int statusFail = 4;

    private const int mapHeight = 15;
    private const int mapWidth = 15;

    private const int defaultMinerCount = 99;

    private bool isLeftButtonDown = false;
    private bool isRightButtonDown = false;

    private float deltaTime = 0;

    public bool[,,] openMap = null;
    public bool[,,] minerMap = null;
    public int[,,] tagMap = null;
    public GameObject[,,] tagObjMap = null;
    public int status;
    public int flagCount;
    public Text textStatus;
    public Text textBigStatus;
    public Text textTime;
    public Text textMinersLeft;

    public Tilemap map;
    public Tile[] tiles;

    public GameObject[] tags;

    private void Awake()
    {
        map = GetComponent<Tilemap>();
        Restart();
    }

    void Update()
    {
        if (status == statusReady || status == statusRun)
        {
            int entryStatus = status;
            if (Input.GetMouseButtonDown(0))
            {
                isLeftButtonDown = true;
            }
            else if (Input.GetMouseButtonDown(1))
            {
                isRightButtonDown = true;
            }
            if (Input.GetMouseButtonUp(0))
            {
                if (isLeftButtonDown)
                {
                    if (isRightButtonDown)
                    {
                        isRightButtonDown = false;
                        status = TwoButtonDown();
                    }
                    else
                    {
                        status = LeftButtonDown();
                    }
                    isLeftButtonDown = false;
                }
            }
            else if (Input.GetMouseButtonUp(1))
            {
                if (isRightButtonDown)
                {
                    if (isLeftButtonDown)
                    {
                        isLeftButtonDown = false;
                        status = TwoButtonDown();
                    }
                    else
                    {
                        RightButtonDown();
                    }
                    isRightButtonDown = false;
                }

            }
            if (status == statusFail)
            {
                textBigStatus.text = "You are FAILED";
            }
            else if (CheckWin())
            {
                status = statusWin;
                //todo
                textBigStatus.text = "You are WIN";
            }
            else if (status == statusRun)
            {
                deltaTime += Time.deltaTime;
            }
            SetMinersLeftText();
            SetTimeText();
        }
        textStatus.text = StatusToString(status);
    }

    int TwoButtonDown()
    {
        Vector3Int pos = GetTileMapPos();
        if (!IsValidPos(pos))
        {
            return status;
        }
        if (!IsOpen(pos))
        {
            return status;
        }
        int count = tagMap[pos.x, pos.y, pos.z];
        if (count < 1 || count > 12)
        {
            return status;
        }
        Vector3Int[] nearPosList = FindNearPos(pos);
        Vector3Int[] openPosList = new Vector3Int[12];
        int openPosListLength = 0;
        int flagCount = 0;
        bool doOpen = true;

        for (int i = 0; i < 12; i++)
        {
            Vector3Int nearPos = nearPosList[i];
            if (!IsValidPos(nearPos))
            {
                continue;
            }
            if (tagMap[nearPos.x, nearPos.y, nearPos.z] == indexQues)
            {
                doOpen = false;
                break;
            }
            else if (tagMap[nearPos.x, nearPos.y, nearPos.z] == indexFlag)
            {
                flagCount++;
            }
            else if (tagMap[nearPos.x, nearPos.y, nearPos.z] == indexEmpty)
            {
                openPosList[openPosListLength] = nearPos;
                openPosListLength++;
            }
        }
        doOpen = doOpen && (flagCount == count);
        bool openRes = true;
        if (doOpen)
        {
            for (int i = 0; i < openPosListLength; i++)
            {
                openRes = openRes && OpenPos(openPosList[i]);
            }
        }

        RenderBg();
        return openRes ? statusRun : statusFail;
    }

    int LeftButtonDown()
    {
        Vector3Int pos = GetTileMapPos();
        if (!IsValidPos(pos))
        {
            return status;
        }
        if (IsOpen(pos))
        {
            return status;
        }
        if (tagMap[pos.x, pos.y, pos.z] != indexEmpty)
        {
            return status;
        }
        if (status == statusReady)
        {
            GenerateMiner(pos);
        }
        bool openRes = OpenPos(pos);
        RenderBg();
        return openRes ? statusRun : statusFail;
    }

    int RightButtonDown()
    {

        Vector3Int pos = GetTileMapPos();
        if (!IsValidPos(pos))
        {
            return status;
        }
        if (IsOpen(pos))
        {
            return status;
        }
        if (status == statusReady)
        {
            GenerateMiner(new Vector3Int(-1, -1, -1));
        }
        int currTag = tagMap[pos.x, pos.y, pos.z];
        int nextTag = indexEmpty;
        switch (currTag)
        {
            case indexEmpty:
                nextTag = indexFlag;
                flagCount++;
                break;
            case indexFlag:
                nextTag = indexQues;
                flagCount--;
                break;
            case indexQues:
                nextTag = indexEmpty;
                break;
        }
        UpgradeTag(pos, nextTag);
        return statusRun;
    }

    public void Restart()
    {
        ResetData();
        RenderBg();
    }

    private void ResetData()
    {
        openMap = new bool[mapWidth, mapHeight, 2];
        minerMap = new bool[mapWidth, mapHeight, 2];
        if (tagObjMap == null)
        {
            tagObjMap = new GameObject[mapWidth, mapHeight, 2];
        }
        tagMap = new int[mapWidth, mapHeight, 2];
        for (int i = 0; i < mapWidth; i++)
        {
            for (int j = 0; j < mapHeight; j++)
            {
                for (int k = 0; k < 2; k++)
                {
                    openMap[i, j, k] = false;
                    minerMap[i, j, k] = false;
                    tagMap[i, j, k] = indexEmpty;
                    GameObject tag = tagObjMap[i, j, k];
                    if (tag)
                    {
                        Destroy(tag);
                    }
                    tagObjMap[i, j, k] = null;
                }
            }
        }

        flagCount = 0;
        deltaTime = 0;
        status = statusReady;
        textBigStatus.text = "";
        SetTimeText();
        SetMinersLeftText();
    }

    private void GenerateMiner(Vector3Int exceptPos)
    {
        bool[,,] newMap = new bool[mapWidth, mapHeight, 2];
        do
        {
            for (int i = 0; i < mapWidth; i++)
            {
                for (int j = 0; j < mapHeight; j++)
                {
                    for (int k = 0; k < 2; k++)
                    {
                        newMap[i, j, k] = false;
                    }
                }
            }
            for (int c = 0; c < defaultMinerCount; c++)
            {
                int x, y, z;
                do
                {
                    x = Random.Range(0, mapWidth);
                    y = Random.Range(0, mapHeight);
                    z = Random.Range(0, 2);
                } while (newMap[x, y, z] || ((x == exceptPos.x) && (y == exceptPos.y) && (z == exceptPos.z)));
                newMap[x, y, z] = true;
            }

        } while (!CheckGeneratedMinerMap(newMap));
        minerMap = newMap;
    }

    private bool CheckGeneratedMinerMap(bool[,,] newMap)
    {
        for (int i = 0; i < mapWidth; i++)
        {
            for (int j = 0; j < mapHeight; j++)
            {
                for (int k = 0; k < 2; k++)
                {
                    if (newMap[i, j, k])
                    {
                        Vector3Int[] nearPosList = FindNearPos(new Vector3Int(i, j, k));
                        bool allAreMiners = true;
                        for (int m = 0; (m < 12) && allAreMiners; m++)
                        {
                            Vector3Int pos = nearPosList[m];
                            if (IsValidPos(pos))
                            {
                                allAreMiners &= newMap[pos.x, pos.y, pos.z];
                            }
                        }
                        if (allAreMiners)
                        {
                            return false;
                        }
                    }
                }
            }
        }
        return true;
    }

    private bool CheckWin()
    {
        bool res = true;
        for (int i = 0; i < mapWidth; i++)
        {
            for (int j = 0; j < mapHeight; j++)
            {
                for (int k = 0; k < 2; k++)
                {
                    res = res && (openMap[i, j, k] == !minerMap[i, j, k]);
                }
            }
        }
        return res;
    }

    private bool OpenPos(Vector3Int pos)
    {
        int x = pos.x;
        int y = pos.y;
        int z = pos.z;
        if (openMap[x, y, z])
        {
            return true;
        }
        openMap[x, y, z] = true;
        if (minerMap[x, y, z])
        {
            UpgradeTag(pos, indexMiner);
            return false;
        }
        Vector3Int[] nearPosList = FindNearPos(pos);
        int nearMinerCount = 0;
        for (int i = 0; i < 12; i++)
        {
            Vector3Int nearPos = nearPosList[i];
            if (IsValidPos(nearPos) && minerMap[nearPos.x, nearPos.y, nearPos.z])
            {
                nearMinerCount++;
            }
        }
        if (nearMinerCount == 0)
        {
            for (int i = 0; i < 12; i++)
            {
                Vector3Int nearPos = nearPosList[i];
                if (IsValidPos(nearPos))
                {
                    OpenPos(nearPos);

                }
            }
        }
        else
        {
            UpgradeTag(pos, nearMinerCount);
        }
        return true;
    }

    private void RenderBg()
    {
        for (int i = 0; i < mapWidth; i++)
        {
            for (int j = 0; j < mapHeight; j++)
            {
                if (openMap[i, j, 0])
                {
                    if (openMap[i, j, 1])
                    {
                        map.SetTile(new Vector3Int(i, j, 0), tiles[3]);
                    }
                    else
                    {
                        map.SetTile(new Vector3Int(i, j, 0), tiles[1]);
                    }
                }
                else
                {
                    if (openMap[i, j, 1])
                    {
                        map.SetTile(new Vector3Int(i, j, 0), tiles[2]);
                    }
                    else
                    {
                        map.SetTile(new Vector3Int(i, j, 0), tiles[0]);
                    }
                }
            }
        }
    }

    private void UpgradeTag(Vector3Int pos, int tagIndex)
    {
        if (tagObjMap[pos.x, pos.y, pos.z])
        {
            Destroy(tagObjMap[pos.x, pos.y, pos.z]);
            tagMap[pos.x, pos.y, pos.z] = indexEmpty;
            tagObjMap[pos.x, pos.y, pos.z] = null;
        }
        if (tagIndex == indexEmpty)
        {
            return;
        }
        GameObject tag = Instantiate(tags[tagIndex]) as GameObject;
        Vector3 center = map.GetCellCenterWorld(new Vector3Int(pos.x, pos.y, 0));
        if (pos.z == 0)
        {
            center.y -= 0.25f;
        }
        else
        {
            center.y += 0.25f;
        }
        tag.transform.Translate(center);
        tagMap[pos.x, pos.y, pos.z] = tagIndex;
        tagObjMap[pos.x, pos.y, pos.z] = tag;
    }

    private Vector3Int GetTileMapPos()
    {
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector3Int vp = map.WorldToCell(mousePos);
        mousePos = map.WorldToLocal(mousePos);

        float x = mousePos.x - Mathf.Floor(mousePos.x);
        float y = mousePos.y - Mathf.Floor(mousePos.y);
        vp.z = x < y ? 1 : 0;
        return vp;
    }

    private Vector3Int[] FindNearPos(Vector3Int pos)
    {
        Vector3Int[] list = new Vector3Int[12];
        int x = pos.x;
        int y = pos.y;
        int z = pos.z;
        if (z == 1)
        {
            list[0] = new Vector3Int(x - 1, y + 1, 0);
            list[1] = new Vector3Int(x, y + 1, 1);
            list[2] = new Vector3Int(x, y + 1, 0);
            list[3] = new Vector3Int(x + 1, y + 1, 1);
            list[4] = new Vector3Int(x + 1, y + 1, 0);
            list[5] = new Vector3Int(x - 1, y, 1);
            list[6] = new Vector3Int(x - 1, y, 0);
            list[7] = new Vector3Int(x, y, 0);
            list[8] = new Vector3Int(x + 1, y, 1);
            list[9] = new Vector3Int(x - 1, y - 1, 1);
            list[10] = new Vector3Int(x - 1, y - 1, 0);
            list[11] = new Vector3Int(x, y - 1, 1);
        }
        else
        {
            list[0] = new Vector3Int(x, y + 1, 0);
            list[1] = new Vector3Int(x + 1, y + 1, 1);
            list[2] = new Vector3Int(x + 1, y + 1, 0);
            list[3] = new Vector3Int(x - 1, y, 0);
            list[4] = new Vector3Int(x, y, 1);
            list[5] = new Vector3Int(x + 1, y, 1);
            list[6] = new Vector3Int(x + 1, y, 0);
            list[7] = new Vector3Int(x - 1, y - 1, 1);
            list[8] = new Vector3Int(x - 1, y - 1, 0);
            list[9] = new Vector3Int(x, y - 1, 1);
            list[10] = new Vector3Int(x, y - 1, 0);
            list[11] = new Vector3Int(x + 1, y - 1, 1);
        }
        return list;
    }

    private bool IsValidPos(Vector3Int pos)
    {
        return pos.x >= 0 && pos.y >= 0 && pos.x < mapWidth && pos.y < mapHeight;
    }

    private bool IsOpen(Vector3Int pos)
    {
        return IsValidPos(pos) && openMap[pos.x, pos.y, pos.z];
    }

    private string StatusToString(int status)
    {
        string res = "";
        switch (status)
        {
            case statusReady:
                res = "READY";
                break;
            case statusRun:
                res = "RUN";
                break;
            case statusFail:
                res = "FAIL";
                break;
            case statusWin:
                res = "WIN";
                break;
        }
        return res;
    }

    private void SetTimeText()
    {
        int sec = Mathf.FloorToInt(deltaTime);
        float milli = deltaTime - sec;
        int min = sec / 60;
        sec = sec - min * 60;
        textTime.text = "Time: " + min.ToString("00") + ":" + sec.ToString("00") + milli.ToString(".000");
    }

    private void SetMinersLeftText()
    {
        int minersLeft = defaultMinerCount - flagCount;
        textMinersLeft.text = "Miners Left: " + minersLeft.ToString();
    }
}
