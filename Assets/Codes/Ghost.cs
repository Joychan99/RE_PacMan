using System.Collections.Generic;
using UnityEngine;

// 유령 AI: 추격(Chase) / 흩어짐(Scatter) / 겁먹음(Frightened) / 먹힘(집으로 복귀) 상태를 가진다.
// 각 분기점에서 목표 타일에 가장 가까워지는 방향을 고른다(역주행은 원칙적으로 금지).
public class Ghost : GridMover
{
    private static readonly Vector2[] Directions =
    {
        Vector2.up,
        Vector2.down,
        Vector2.left,
        Vector2.right
    };

    public Color baseColor = Color.red;
    public int aheadOffset = 0;        // 팩맨 진행방향 앞쪽 몇 칸을 노릴지 (성격 차이)
    public Vector2Int scatterCorner;   // 흩어짐 모드에서 향하는 구석 (row, col)

    private int homeRow, homeCol;      // 스폰(집) 위치
    private int exitRow, exitCol;      // 집 밖 출구(문 위 칸)
    private bool inHouse = true;       // 아직 집 안에 있는가
    private bool eaten;                // true면 집으로 복귀 중(눈알 상태)
    private SpriteRenderer sr;

    protected override bool IsGhost => true;

    public void Init(int row, int col, Color color, int ahead, Vector2Int corner)
    {
        Row = row; Col = col; homeRow = row; homeCol = col;
        baseColor = color; aheadOffset = ahead; scatterCorner = corner;
    }

    public void SetHouseExit(int row, int col) { exitRow = row; exitCol = col; }

    protected override void Start()
    {
        base.Start();
        sr = GetComponent<SpriteRenderer>();
        speed = 4.2f;
    }

    public void ResetToHome()
    {
        eaten = false;
        inHouse = true;
        SetCell(homeRow, homeCol);
    }

    public bool IsEaten => eaten;
    public void GetEaten() { eaten = true; } // 겁먹은 상태에서 팩맨에게 먹혔을 때

    protected override void DecideDirection()
    {
        UpdateAppearance();

        if (eaten)
        {
            nextDirection = FindStepToward(homeRow, homeCol);
            speed = 7f;
            if (nextDirection != Vector2.zero) return;
        }

        // 현재 칸에서 갈 수 있는 방향들(역주행 제외)
        Vector2[] options = new Vector2[4];
        int optionCount = 0;
        foreach (var d in Directions)
        {
            if (CanMove(d) && d != -Direction)
                options[optionCount++] = d;
        }

        // 막다른 길이면 역주행 허용
        if (optionCount == 0 && Direction != Vector2.zero && CanMove(-Direction))
            options[optionCount++] = -Direction;
        if (optionCount == 0) { nextDirection = Vector2.zero; return; }

        // 겁먹음(아직 안 먹힌 상태)이면 무작위 이동
        if (gm.Frightened && !eaten)
        {
            nextDirection = options[Random.Range(0, optionCount)];
            return;
        }

        // 목표 타일 결정
        int tRow, tCol;
        GetTarget(out tRow, out tCol);

        // 목표에 가장 가까워지는 방향 선택
        float best = float.MaxValue;
        Vector2 chosen = options[0];
        for (int i = 0; i < optionCount; i++)
        {
            Vector2 d = options[i];
            int nCol = Col + Mathf.RoundToInt(d.x);
            int nRow = Row - Mathf.RoundToInt(d.y);
            float dist = (nRow - tRow) * (nRow - tRow) + (nCol - tCol) * (nCol - tCol);
            if (dist < best) { best = dist; chosen = d; }
        }
        nextDirection = chosen;

        // 속도: 먹힘 > 평소 > 겁먹음
        speed = eaten ? 7f : (gm.Frightened ? 3f : 4.2f);
    }

    private Vector2 FindStepToward(int targetRow, int targetCol)
    {
        if (Row == targetRow && Col == targetCol) return Vector2.zero;

        int rows = gm.Rows;
        int cols = gm.Cols;
        bool[,] visited = new bool[rows, cols];
        int[,] prevRow = new int[rows, cols];
        int[,] prevCol = new int[rows, cols];

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                prevRow[r, c] = -1;
                prevCol[r, c] = -1;
            }

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(Row, Col));
        visited[Row, Col] = true;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (current.x == targetRow && current.y == targetCol)
                break;

            foreach (var d in Directions)
            {
                int nextCol = current.y + Mathf.RoundToInt(d.x);
                int nextRow = current.x - Mathf.RoundToInt(d.y);

                if (nextRow < 0 || nextCol < 0 || nextRow >= rows || nextCol >= cols)
                    continue;
                if (visited[nextRow, nextCol])
                    continue;
                if (gm.Blocked(nextRow, nextCol, true))
                    continue;

                visited[nextRow, nextCol] = true;
                prevRow[nextRow, nextCol] = current.x;
                prevCol[nextRow, nextCol] = current.y;
                queue.Enqueue(new Vector2Int(nextRow, nextCol));
            }
        }

        if (!visited[targetRow, targetCol]) return Vector2.zero;

        int stepRow = targetRow;
        int stepCol = targetCol;
        while (prevRow[stepRow, stepCol] != Row || prevCol[stepRow, stepCol] != Col)
        {
            int parentRow = prevRow[stepRow, stepCol];
            int parentCol = prevCol[stepRow, stepCol];
            if (parentRow < 0 || parentCol < 0) return Vector2.zero;
            stepRow = parentRow;
            stepCol = parentCol;
        }

        return new Vector2(stepCol - Col, Row - stepRow);
    }

    private void GetTarget(out int tRow, out int tCol)
    {
        if (eaten) { tRow = homeRow; tCol = homeCol; return; }      // 집으로
        if (inHouse) { tRow = exitRow; tCol = exitCol; return; }    // 일단 집 밖으로
        if (gm.GhostScatter) { tRow = scatterCorner.x; tCol = scatterCorner.y; return; } // 구석으로

        // 추격: 팩맨 위치 + 진행방향 앞 offset 칸
        Vector2 pd = gm.PacmanDirection;
        tCol = gm.PacmanCol + Mathf.RoundToInt(pd.x) * aheadOffset;
        tRow = gm.PacmanRow - Mathf.RoundToInt(pd.y) * aheadOffset;
    }

    protected override void OnArrivedAtCell()
    {
        // 출구(문 위)에 도달하면 집에서 나온 것으로 처리
        if (inHouse && Row <= exitRow) inHouse = false;

        // 먹힌 상태로 집에 도착하면 부활(다시 집에서 나가야 함)
        if (eaten && Row == homeRow && Col == homeCol)
        {
            eaten = false;
            inHouse = true;
            speed = gm != null && gm.Frightened ? 3f : 4.2f;
        }
    }

    private void UpdateAppearance()
    {
        if (sr == null) return;
        if (eaten)            sr.color = new Color(0.5f, 0.5f, 0.5f, 0.5f); // 눈알(반투명)
        else if (gm.Frightened) sr.color = new Color(0.25f, 0.35f, 1f);    // 파랗게
        else                  sr.color = baseColor;
    }
}