using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 게임 전체를 코드로 생성·관리한다.
// 빈 GameObject 하나에 이 스크립트만 붙이고 Play 하면 미로/팩맨/유령/점수까지 모두 생성된다.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // 미로 기호: # 벽 / . 먹이 / o 파워먹이 / - 유령문 / P 팩맨시작 / G 유령시작 / (공백) 빈칸
    private static readonly string[] mapA =
    {
        "###################",
        "#........#........#",
        "#o##.###.#.###.##o#",
        "#.................#",
        "#.##.#.#####.#.##.#",
        "#....#...#...#....#",
        "####.###.#.###.####",
        "####.#.......#.####",
        "####.#.##-##.#.####",
        "#......#GGG#......#",
        "####.#.#####.#.####",
        "####.#.......#.####",
        "####.#.#####.#.####",
        "#........#........#",
        "#.##.###.#.###.##.#",
        "#o.#.....P.....#.o#",
        "##.#.#.#####.#.#.##",
        "#....#...#...#....#",
        "#.######.#.######.#",
        "#.................#",
        "###################",
    };

    private static readonly string[] mapB = BuildMapB();
    private static readonly string[] mapC = BuildMapC();

    private readonly string[][] mazes = { mapA, mapB, mapC };

    public int Rows { get; private set; }
    public int Cols { get; private set; }

    private char[,] tiles;                 // 벽/문 판정용
    private GameObject[,] pellets;         // 먹이 오브젝트 참조(먹으면 파괴)
    private readonly List<Transform> powerPellets = new List<Transform>();

    // 상태
    public int Score { get; private set; }
    public int Lives { get; private set; } = 3;
    public int PelletsRemaining { get; private set; }
    public bool Paused { get; private set; }   // 게임오버/승리 시 정지
    private bool gameOver, won;

    // 유령 모드(겁먹음 / 흩어짐<->추격 교대)
    public bool Frightened { get; private set; }
    private float frightenedTimer;
    public float frightenedDuration = 7f;
    public bool GhostScatter { get; private set; } = false;

    private Pacman pacman;
    private int pacSpawnRow, pacSpawnCol;
    private readonly List<Ghost> ghosts = new List<Ghost>();
    private readonly List<Vector2Int> ghostSpawns = new List<Vector2Int>();
    private float spawnProtectionTimer;

    // 스프라이트(코드 생성)
    private Sprite squareSprite, circleSprite, pacmanSprite, ghostSprite;
    private Texture2D hudPanelTexture, hudAccentTexture, lifeDotTexture;
    private Texture2D mapBackdropTexture, mapCardTexture, mapCardHoverTexture, mapCardAccentTexture;
    private GUIStyle hudTitleStyle, hudValueStyle, hudSmallStyle, hudEndTitleStyle, hudEndSubStyle;
    private GUIStyle mapTitleStyle, mapSubStyle, mapHintStyle, mapCardLetterStyle, mapCardTitleStyle, mapCardDescStyle, mapCardButtonStyle;

    private string[] activeMaze;
    private bool gameStarted;

    public int PacmanRow => pacman != null ? pacman.Row : 0;
    public int PacmanCol => pacman != null ? pacman.Col : 0;
    public Vector2 PacmanDirection => pacman != null ? pacman.Direction : Vector2.zero;

    // ---------- 초기화 ----------
    void Awake()
    {
        Instance = this;

        squareSprite  = MakeSquareSprite();
        circleSprite  = MakeCircleSprite(48);
        pacmanSprite  = MakePacmanSprite(48);
        ghostSprite   = MakeGhostSprite(48);

        BuildHudAssets();
    }

    static string[] BuildMapB()
    {
        var map = CloneMap(mapA);
        OpenRowRange(map, 1, 2, 16);
        OpenRowRange(map, 2, 2, 16);
        OpenRowRange(map, 3, 2, 16);
        OpenRowRange(map, 5, 2, 16);
        OpenRowRange(map, 6, 3, 15);
        OpenRowRange(map, 7, 3, 15);
        OpenRowRange(map, 8, 3, 15);
        OpenRowRange(map, 9, 2, 16);
        OpenRowRange(map, 10, 3, 15);
        OpenRowRange(map, 11, 3, 15);
        OpenRowRange(map, 12, 3, 15);
        OpenRowRange(map, 13, 2, 16);
        OpenRowRange(map, 14, 2, 16);
        OpenRowRange(map, 18, 2, 16);
        SetRow(map, 7, mapA[7]);
        SetRow(map, 8, mapA[8]);
        SetRow(map, 9, mapA[9]);
        SetRow(map, 10, mapA[10]);
        SetRow(map, 15, mapA[15]);
        return ToStringRows(map);
    }

    static string[] BuildMapC()
    {
        var map = CloneMap(mapA);
        OpenColumnRange(map, 3, 1, 18);
        OpenColumnRange(map, 15, 2, 19);
        OpenRowRange(map, 4, 3, 15);
        OpenRowRange(map, 10, 3, 15);
        OpenRowRange(map, 15, 3, 15);
        OpenRowRange(map, 18, 3, 15);
        SetRow(map, 8, mapA[8]);
        SetRow(map, 9, mapA[9]);
        SetRow(map, 10, mapA[10]);
        SetRow(map, 15, mapA[15]);
        return ToStringRows(map);
    }

    static char[][] CloneMap(string[] source)
    {
        var clone = new char[source.Length][];
        for (int i = 0; i < source.Length; i++)
            clone[i] = source[i].ToCharArray();
        return clone;
    }

    static void SetCell(char[][] map, int row, int col, char value)
    {
        if (row < 0 || row >= map.Length) return;
        if (col < 0 || col >= map[row].Length) return;
        map[row][col] = value;
    }

    static void SetRow(char[][] map, int row, string value)
    {
        if (row < 0 || row >= map.Length) return;
        if (value == null) return;
        int limit = Mathf.Min(map[row].Length, value.Length);
        for (int col = 0; col < limit; col++)
            map[row][col] = value[col];
    }

    static void OpenRowRange(char[][] map, int row, int startCol, int endCol)
    {
        for (int col = startCol; col <= endCol; col++)
            SetCell(map, row, col, ' ');
    }

    static void OpenColumnRange(char[][] map, int col, int startRow, int endRow)
    {
        for (int row = startRow; row <= endRow; row++)
            SetCell(map, row, col, ' ');
    }

    static string[] ToStringRows(char[][] map)
    {
        var rows = new string[map.Length];
        for (int i = 0; i < map.Length; i++)
            rows[i] = new string(map[i]);
        return rows;
    }

    void StartGame(int mapIndex)
    {
        activeMaze = mazes[Mathf.Clamp(mapIndex, 0, mazes.Length - 1)];
        Rows = activeMaze.Length;
        Cols = activeMaze[0].Length;

        Score = 0;
        Lives = 3;
        PelletsRemaining = 0;
        Paused = false;
        gameOver = false;
        won = false;
        Frightened = false;
        GhostScatter = false;
        frightenedTimer = 0f;
        spawnProtectionTimer = 1.2f;

        ghosts.Clear();
        ghostSpawns.Clear();

        BuildLevel();
        SpawnPacman();
        SpawnGhosts();
        SetupCamera();
        gameStarted = true;
    }

    public Vector3 CellToWorld(int row, int col)
        => new Vector3(col, (Rows - 1) - row, 0f);

    public bool Blocked(int row, int col, bool isGhost)
    {
        if (row < 0 || col < 0 || row >= Rows || col >= Cols) return true;
        char ch = tiles[row, col];
        if (ch == '#') return true;
        if (ch == '-') return !isGhost;   // 문은 유령만 통과
        return false;
    }

    void BuildLevel()
    {
        if (activeMaze == null) return;

        tiles = new char[Rows, Cols];
        pellets = new GameObject[Rows, Cols];
        PelletsRemaining = 0;

        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                char ch = activeMaze[r][c];
                tiles[r, c] = ch;
                Vector3 pos = CellToWorld(r, c);

                switch (ch)
                {
                    case '#':
                        MakeSprite("Wall", pos, squareSprite,
                                   new Color(0.13f, 0.18f, 0.9f), 0.92f, 0);
                        break;
                    case '-':
                        MakeSprite("Door", pos + new Vector3(0, 0.0f, 0), squareSprite,
                                   new Color(1f, 0.6f, 0.8f), 0.05f, 0)
                                   .transform.localScale = new Vector3(0.92f, 0.12f, 1f);
                        break;
                    case '.':
                        pellets[r, c] = MakeSprite("Pellet", pos, circleSprite,
                                   new Color(1f, 0.85f, 0.6f), 0.16f, 1);
                        PelletsRemaining++;
                        break;
                    case 'o':
                        var pp = MakeSprite("PowerPellet", pos, circleSprite,
                                   new Color(1f, 0.85f, 0.6f), 0.5f, 1);
                        pellets[r, c] = pp;
                        powerPellets.Add(pp.transform);
                        PelletsRemaining++;
                        break;
                    case 'P':
                        pacSpawnRow = r; pacSpawnCol = c;
                        break;
                    case 'G':
                        ghostSpawns.Add(new Vector2Int(r, c));
                        break;
                }
            }
        }
    }

    void SpawnPacman()
    {
        var go = new GameObject("Pacman");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = pacmanSprite;
        sr.color = new Color(1f, 0.92f, 0.15f);
        sr.sortingOrder = 2;
        go.transform.localScale = Vector3.one * 0.85f;
        pacman = go.AddComponent<Pacman>();
        pacman.speed = 5.2f;
        pacman.Row = pacSpawnRow; pacman.Col = pacSpawnCol;
    }

    void SpawnGhosts()
    {
        // 문('-') 위치를 찾아 그 위 칸을 유령 출구로 사용
        int doorRow = 8, doorCol = Cols / 2;
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
                if (activeMaze[r][c] == '-') { doorRow = r; doorCol = c; }
        int exitRow = doorRow - 1, exitCol = doorCol;

        // (색, 앞칸offset, 흩어짐 구석)
        Color[] colors = { new Color(1f,0.2f,0.2f), new Color(1f,0.55f,0.85f), new Color(0.3f,0.9f,1f) };
        int[] aheads = { 0, 4, 2 };
        Vector2Int[] corners = {
            new Vector2Int(0, Cols - 1),   // 우상단
            new Vector2Int(0, 0),          // 좌상단
            new Vector2Int(Rows - 1, Cols-1) // 우하단
        };

        for (int i = 0; i < ghostSpawns.Count && i < 3; i++)
        {
            var sp = ghostSpawns[i];
            var go = new GameObject("Ghost" + i);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ghostSprite;
            sr.sortingOrder = 2;
            go.transform.localScale = Vector3.one * 0.8f;
            var g = go.AddComponent<Ghost>();
            g.Init(sp.x, sp.y, colors[i], aheads[i], corners[i]);
            g.SetHouseExit(exitRow, exitCol);
            ghosts.Add(g);
        }
    }

    void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera");
            cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
        }
        cam.orthographic = true;
        cam.backgroundColor = Color.black;
        cam.transform.position = new Vector3((Cols - 1) / 2f, (Rows - 1) / 2f, -10f);
        float vert = Rows / 2f + 0.5f;
        float horiz = (Cols / 2f + 0.5f) / Mathf.Max(0.0001f, cam.aspect);
        cam.orthographicSize = Mathf.Max(vert, horiz);
    }

    // ---------- 게임 루프 ----------
    void Update()
    {
        if (!gameStarted)
        {
            if (Input.GetKeyDown(KeyCode.A)) StartGame(0);
            else if (Input.GetKeyDown(KeyCode.B)) StartGame(1);
            else if (Input.GetKeyDown(KeyCode.C)) StartGame(2);
            return;
        }

        if (gameOver || won)
        {
            if (Input.GetKeyDown(KeyCode.R))
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        // 겁먹음 타이머
        if (Frightened)
        {
            frightenedTimer -= Time.deltaTime;
            if (frightenedTimer <= 0f) Frightened = false;
        }
        else
        {
            GhostScatter = false;
        }

        if (spawnProtectionTimer > 0f)
            spawnProtectionTimer -= Time.deltaTime;

        // 파워먹이 깜빡임
        float s = 0.5f + Mathf.Sin(Time.time * 6f) * 0.12f;
        foreach (var p in powerPellets)
            if (p != null) p.localScale = Vector3.one * s;

        CheckCollisions();
    }

    public void EatPellet(int row, int col)
    {
        if (pellets[row, col] == null) return;
        char ch = tiles[row, col];
        if (ch != '.' && ch != 'o') return;

        Destroy(pellets[row, col]);
        pellets[row, col] = null;
        tiles[row, col] = ' ';
        PelletsRemaining--;

        if (ch == 'o')
        {
            Score += 50;
            Frightened = true;
            frightenedTimer = frightenedDuration;
        }
        else Score += 10;

        if (PelletsRemaining <= 0) { won = true; Paused = true; }
    }

    void CheckCollisions()
    {
        if (spawnProtectionTimer > 0f) return;
        if (pacman == null) return;
        foreach (var g in ghosts)
        {
            if (g.IsEaten) continue;
            float d = (g.transform.position - pacman.transform.position).sqrMagnitude;
            if (d < 0.25f) // 약 0.5칸 이내
            {
                if (Frightened) { g.GetEaten(); Score += 200; }
                else PacmanHit();
            }
        }
    }

    void PacmanHit()
    {
        Lives--;
        if (Lives <= 0) { gameOver = true; Paused = true; return; }

        // 위치 리셋
        pacman.SetCell(pacSpawnRow, pacSpawnCol);
        foreach (var g in ghosts) g.ResetToHome();
        Frightened = false;
        GhostScatter = false;
        spawnProtectionTimer = 1.2f;
    }

    // ---------- 간단한 화면 UI ----------
    void OnGUI()
    {
        if (!gameStarted)
        {
            DrawMapSelect();
            return;
        }

        DrawHud();

        if (won || gameOver)
        {
            string msg = won ? "YOU WIN!" : "GAME OVER";
            hudEndTitleStyle.normal.textColor = won ? new Color(1f, 0.92f, 0.35f) : new Color(1f, 0.35f, 0.35f);
            GUI.Label(new Rect(0, Screen.height * 0.42f - 40f, Screen.width, 50f), msg, hudEndTitleStyle);
            GUI.Label(new Rect(0, Screen.height * 0.42f + 10f, Screen.width, 30f), "Press R to restart", hudEndSubStyle);
        }
    }

    void DrawMapSelect()
    {
        float panelWidth = 660f;
        float panelHeight = 420f;
        float x = (Screen.width - panelWidth) * 0.5f;
        float y = (Screen.height - panelHeight) * 0.5f;

        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), mapBackdropTexture, ScaleMode.StretchToFill);
        GUI.DrawTexture(new Rect(x + 6f, y + 8f, panelWidth, panelHeight), hudPanelTexture, ScaleMode.StretchToFill);
        GUI.DrawTexture(new Rect(x, y, panelWidth, panelHeight), hudPanelTexture, ScaleMode.StretchToFill);
        GUI.color = new Color(1f, 0.76f, 0.18f, 1f);
        GUI.DrawTexture(new Rect(x, y, panelWidth, 8f), mapCardAccentTexture, ScaleMode.StretchToFill);
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(x, y, 10f, panelHeight), hudAccentTexture, ScaleMode.StretchToFill);

        GUI.Label(new Rect(x + 24f, y + 18f, panelWidth - 48f, 36f), "SELECT MAP", mapTitleStyle);
        GUI.Label(new Rect(x + 24f, y + 58f, panelWidth - 48f, 24f), "Choose a stage before the chase begins.", mapSubStyle);
        GUI.Label(new Rect(x + panelWidth - 170f, y + 30f, 146f, 18f), "A / B / C", mapHintStyle);

        float buttonY = y + 116f;
        float buttonWidth = 178f;
        float buttonHeight = 214f;
        float gap = 18f;
        float startX = x + 32f;

        DrawMapCard(new Rect(startX, buttonY, buttonWidth, buttonHeight), "A", "Classic Maze", "Balanced lanes and familiar flow.", new Color(0.98f, 0.74f, 0.18f, 1f), 0);
        DrawMapCard(new Rect(startX + buttonWidth + gap, buttonY, buttonWidth, buttonHeight), "B", "Open Arena", "Wide corridors and faster routes.", new Color(0.35f, 0.86f, 1f, 1f), 1);
        DrawMapCard(new Rect(startX + (buttonWidth + gap) * 2f, buttonY, buttonWidth, buttonHeight), "C", "Twin Lanes", "Vertical channels and tighter turns.", new Color(1f, 0.45f, 0.82f, 1f), 2);

        GUI.Label(new Rect(x + 24f, y + panelHeight - 28f, panelWidth - 48f, 18f), "Click a card or press A, B, C.", mapSubStyle);
    }

    void DrawMapCard(Rect rect, string letter, string title, string description, Color accentColor, int mapIndex)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.35f);
        GUI.DrawTexture(new Rect(rect.x + 4f, rect.y + 5f, rect.width, rect.height), hudPanelTexture, ScaleMode.StretchToFill);
        GUI.color = Color.white;

        GUI.DrawTexture(rect, mapCardTexture, ScaleMode.StretchToFill);
        GUI.color = accentColor;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 10f), mapCardAccentTexture, ScaleMode.StretchToFill);
        GUI.color = Color.white;

        if (GUI.Button(rect, GUIContent.none, mapCardButtonStyle))
            StartGame(mapIndex);

        GUI.Label(new Rect(rect.x + 16f, rect.y + 18f, 40f, 36f), letter, mapCardLetterStyle);
        GUI.Label(new Rect(rect.x + 16f, rect.y + 62f, rect.width - 32f, 28f), title, mapCardTitleStyle);
        GUI.Label(new Rect(rect.x + 16f, rect.y + 94f, rect.width - 32f, 56f), description, mapCardDescStyle);
    }

    void DrawHud()
    {
        if (hudPanelTexture == null) BuildHudAssets();

        float margin = 12f;
        float panelWidth = 292f;
        float panelHeight = 96f;
        Rect panel = new Rect(margin, margin, panelWidth, panelHeight);
        Rect shadow = new Rect(panel.x + 3f, panel.y + 4f, panel.width, panel.height);
        GUI.color = new Color(0f, 0f, 0f, 0.35f);
        GUI.DrawTexture(shadow, hudPanelTexture, ScaleMode.StretchToFill);
        GUI.color = Color.white;
        GUI.DrawTexture(panel, hudPanelTexture, ScaleMode.StretchToFill);

        Rect accent = new Rect(panel.x, panel.y, 10f, panel.height);
        GUI.color = new Color(1f, 0.74f, 0.16f, 0.98f);
        GUI.DrawTexture(accent, hudAccentTexture, ScaleMode.StretchToFill);
        GUI.color = Color.white;

        GUI.Label(new Rect(panel.x + 18f, panel.y + 10f, 120f, 20f), "SCORE", hudTitleStyle);
        DrawShadowLabel(new Rect(panel.x + 18f, panel.y + 27f, 210f, 42f), Score.ToString("00000"), hudValueStyle);

        GUI.Label(new Rect(panel.x + 168f, panel.y + 10f, 78f, 20f), "LIVES", hudTitleStyle);
        for (int i = 0; i < 3; i++)
        {
            bool active = i < Lives;
            Color heartColor = active ? new Color(0.98f, 0.18f, 0.22f, 1f) : new Color(0.52f, 0.56f, 0.61f, 0.9f);
            float x = panel.x + 176f + (i * 25f);
            float y = panel.y + 37f;
            GUI.color = heartColor;
            GUI.DrawTexture(new Rect(x, y, 20f, 20f), lifeDotTexture, ScaleMode.StretchToFill, true);
        }
        GUI.color = Color.white;
    }

    void DrawShadowLabel(Rect rect, string text, GUIStyle style)
    {
        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
        GUI.color = prev;
        GUI.Label(rect, text, style);
    }

    void BuildHudAssets()
    {
        if (hudPanelTexture != null) return;

        hudPanelTexture = CreateSolidTexture(new Color(0.09f, 0.11f, 0.17f, 0.92f));
        hudAccentTexture = CreateSolidTexture(Color.white);
        lifeDotTexture = CreateCircleTexture(64);
        mapBackdropTexture = CreateSolidTexture(new Color(0.02f, 0.04f, 0.08f, 0.94f));
        mapCardTexture = CreateSolidTexture(new Color(0.11f, 0.14f, 0.2f, 0.96f));
        mapCardHoverTexture = CreateSolidTexture(new Color(0.18f, 0.22f, 0.32f, 0.98f));
        mapCardAccentTexture = CreateSolidTexture(Color.white);

        hudTitleStyle = new GUIStyle
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.78f, 0.83f, 0.94f, 1f) }
        };

        hudValueStyle = new GUIStyle
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(1f, 0.95f, 0.55f, 1f) }
        };

        hudSmallStyle = new GUIStyle
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        hudEndTitleStyle = new GUIStyle
        {
            fontSize = 32,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        hudEndSubStyle = new GUIStyle
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.92f, 0.92f, 0.92f, 1f) }
        };

        mapTitleStyle = new GUIStyle
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(1f, 0.95f, 0.82f, 1f) }
        };

        mapSubStyle = new GUIStyle
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.82f, 0.86f, 0.94f, 1f) }
        };

        mapHintStyle = new GUIStyle
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = new Color(1f, 0.8f, 0.2f, 1f) }
        };

        mapCardLetterStyle = new GUIStyle
        {
            fontSize = 34,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = Color.white }
        };

        mapCardTitleStyle = new GUIStyle
        {
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft,
            wordWrap = true,
            normal = { textColor = new Color(1f, 0.94f, 0.7f, 1f) }
        };

        mapCardDescStyle = new GUIStyle
        {
            fontSize = 12,
            alignment = TextAnchor.UpperLeft,
            wordWrap = true,
            normal = { textColor = new Color(0.84f, 0.88f, 0.96f, 1f) }
        };

        mapCardButtonStyle = new GUIStyle
        {
            normal = { background = mapCardTexture, textColor = Color.white },
            hover = { background = mapCardHoverTexture, textColor = Color.white },
            active = { background = mapCardHoverTexture, textColor = Color.white },
            border = new RectOffset(0, 0, 0, 0),
            alignment = TextAnchor.UpperLeft
        };
    }

    static Texture2D CreateSolidTexture(Color color)
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }

    static Texture2D CreateCircleTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float r = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r;
                float dy = y + 0.5f - r;
                bool inside = dx * dx + dy * dy <= (r - 0.5f) * (r - 0.5f);
                tex.SetPixel(x, y, inside ? Color.white : Color.clear);
            }
        }

        tex.Apply();
        return tex;
    }

    // ---------- 스프라이트 생성 유틸 ----------
    GameObject MakeSprite(string name, Vector3 pos, Sprite sprite, Color color, float scale, int order)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return go;
    }

    static Sprite MakeSquareSprite()
    {
        var tex = new Texture2D(4, 4);
        var px = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = Color.white;
        tex.SetPixels(px); tex.Apply();
        tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }

    static Sprite MakeCircleSprite(int size)
    {
        var tex = new Texture2D(size, size);
        float r = size / 2f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                px[y * size + x] = (dx * dx + dy * dy <= (r - 0.5f) * (r - 0.5f))
                                   ? Color.white : Color.clear;
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    static Sprite MakePacmanSprite(int size)
    {
        var tex = new Texture2D(size, size);
        float r = size / 2f;
        float mouth = 32f * Mathf.Deg2Rad; // 입 벌림(반각)
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                bool inside = dx * dx + dy * dy <= (r - 0.5f) * (r - 0.5f);
                float ang = Mathf.Atan2(dy, dx);          // +x 방향이 0
                if (Mathf.Abs(ang) < mouth) inside = false; // +x 쪽 쐐기를 잘라 입 모양
                px[y * size + x] = inside ? Color.white : Color.clear;
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    static Sprite MakeGhostSprite(int size)
    {
        var tex = new Texture2D(size, size);
        float r = size / 2f;
        float domeCy = size * 0.55f; // 위쪽 반원 중심
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r;
                bool inside;
                if (y >= domeCy)
                {
                    float dy = y + 0.5f - domeCy;       // 위쪽: 반원(머리)
                    inside = dx * dx + dy * dy <= (r - 0.5f) * (r - 0.5f);
                }
                else
                {
                    // 아래쪽: 사각 몸통 + 물결 모양 밑단
                    inside = Mathf.Abs(dx) <= r - 0.5f;
                    if (inside && y < size * 0.16f)
                    {
                        float wave = Mathf.Sin((x / (float)size) * Mathf.PI * 4f);
                        if (wave < -0.2f) inside = false;
                    }
                }
                px[y * size + x] = inside ? Color.white : Color.clear;
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}