namespace RazerKeyboard;

// Generates per-frame color grids for the Razer keyboard CHROMA_CUSTOM effect.
// Layout: 6 rows × 22 columns (standard Razer keyboard mapping).
sealed class MatrixRain
{
    const int Rows = 6;
    const int Cols = 22;

    static readonly int[] Palette =
    [
        ChromaClient.Green,
        ChromaClient.Green2,
        ChromaClient.Green3,
        ChromaClient.Green4,
        ChromaClient.Green5,
        ChromaClient.Green6,
        ChromaClient.Black,
    ];

    readonly Random  _rng   = new();
    readonly int[]   _pos   = new int[Cols];
    readonly int[]   _len   = new int[Cols];
    readonly int[]   _speed = new int[Cols];
    readonly int[]   _ticks = new int[Cols];
    readonly int[][] _grid;

    public MatrixRain()
    {
        _grid = new int[Rows][];
        for (int r = 0; r < Rows; r++)
            _grid[r] = new int[Cols];

        for (int c = 0; c < Cols; c++)
        {
            _pos[c]   = _rng.Next(-6, Rows);
            _len[c]   = _rng.Next(3, Rows + 1);
            _speed[c] = _rng.Next(1, 3);
        }
    }

    // Returns a reference to the internal grid — do not retain across frames.
    public int[][] NextFrame()
    {
        for (int r = 0; r < Rows; r++)
            Array.Clear(_grid[r], 0, Cols);

        for (int c = 0; c < Cols; c++)
        {
            int head = _pos[c];
            int len  = _len[c];
            for (int i = 0; i < len; i++)
            {
                int r = head - i;
                if ((uint)r < Rows)
                    _grid[r][c] = Palette[Math.Min(i, Palette.Length - 1)];
            }
        }

        for (int c = 0; c < Cols; c++)
        {
            if (++_ticks[c] >= _speed[c])
            {
                _ticks[c] = 0;
                if (++_pos[c] - _len[c] > Rows)
                {
                    _pos[c]   = _rng.Next(-6, -1);
                    _len[c]   = _rng.Next(3, Rows + 1);
                    _speed[c] = _rng.Next(1, 3);
                }
            }
        }

        return _grid;
    }
}
