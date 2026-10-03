using System.Collections;
using MinikDuello.Domain.Rounds;
using MinikDuello.Infra;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    /// <summary>Çocuk dostu labirent: karakteri yön tuşlarıyla veya kaydırarak hazineye ulaştır.</summary>
    public sealed class MazeRoundView : RoundView
    {
        private const float AreaSize = 860f;
        private const float WallThickness = 9f;
        private const float MoveSeconds = 0.12f;
        private const float DirectionButton = 150f;

        private MazeRound maze;
        private MazeNavigator nav;
        private float cell;
        private RectTransform area;
        private RectTransform player;
        private Coroutine moving;

        public override void Build(RoundSpec spec)
        {
            maze = (MazeRound)spec;
            nav = new MazeNavigator(maze);
            cell = AreaSize / maze.Width;

            BuildArea();
            BuildPad();
        }

        private void BuildArea()
        {
            var go = new GameObject("MazeArea", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(SwipeArea));
            go.transform.SetParent(transform, false);
            area = (RectTransform)go.transform;
            var bg = go.GetComponent<Image>();
            bg.sprite = SpriteFactory.RoundedRect();
            bg.type = Image.Type.Sliced;
            bg.color = new Color32(255, 255, 255, 235);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = AreaSize;
            le.preferredHeight = AreaSize;
            go.GetComponent<SwipeArea>().Swiped = OnSwipe;

            Color wall = new Color32(120, 90, 70, 255);
            for (int y = 0; y < maze.Height; y++)
            {
                for (int x = 0; x < maze.Width; x++)
                {
                    Wall w = maze.Walls[y * maze.Width + x];
                    if (y == 0 && (w & Wall.North) != 0) Line(x * cell, y * cell, cell + WallThickness, WallThickness, wall);
                    if (x == 0 && (w & Wall.West) != 0) Line(x * cell, y * cell, WallThickness, cell + WallThickness, wall);
                    if ((w & Wall.East) != 0) Line((x + 1) * cell, y * cell, WallThickness, cell + WallThickness, wall);
                    if ((w & Wall.South) != 0) Line(x * cell, (y + 1) * cell, cell + WallThickness, WallThickness, wall);
                }
            }

            Image goal = UIFactory.CreateImage(area, "Goal", UITheme.Yellow);
            goal.sprite = SpriteFactory.Shape("star");
            Place(goal.rectTransform, maze.GoalX, maze.GoalY, 0.7f);

            Image token = UIFactory.CreateImage(area, "Player", UITheme.Primary);
            token.sprite = SpriteFactory.Shape("circle");
            player = token.rectTransform;
            Place(player, nav.X, nav.Y, 0.6f);
        }

        private void Line(float x, float y, float w, float h, Color color)
        {
            Image line = UIFactory.CreateImage(area, "Wall", color);
            line.rectTransform.anchorMin = line.rectTransform.anchorMax = new Vector2(0f, 1f);
            line.rectTransform.pivot = new Vector2(0f, 1f);
            line.rectTransform.anchoredPosition = new Vector2(x - WallThickness * 0.5f, -(y - WallThickness * 0.5f));
            line.rectTransform.sizeDelta = new Vector2(w, h);
        }

        private void Place(RectTransform rect, int cx, int cy, float scale)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.one * cell * scale;
            rect.anchoredPosition = CellCenter(cx, cy);
        }

        private Vector2 CellCenter(int cx, int cy) => new Vector2((cx + 0.5f) * cell, -(cy + 0.5f) * cell);

        private void BuildPad()
        {
            RectTransform column = UIFactory.CreateColumn(transform, 10f, DirectionButton * 3f + 20f);
            RectTransform top = UIFactory.CreateRow(column, 10f, DirectionButton);
            Arrow(top, Wall.North, 0f);
            RectTransform bottom = UIFactory.CreateRow(column, 10f, DirectionButton);
            Arrow(bottom, Wall.West, 90f);
            Arrow(bottom, Wall.South, 180f);
            Arrow(bottom, Wall.East, -90f);
        }

        private void Arrow(Transform parent, Wall direction, float rotation)
        {
            var go = new GameObject("Arrow_" + direction, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var bg = go.GetComponent<Image>();
            bg.sprite = SpriteFactory.RoundedRect();
            bg.type = Image.Type.Sliced;
            bg.color = UITheme.Blue;
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = DirectionButton;
            le.preferredHeight = DirectionButton;
            go.GetComponent<Button>().targetGraphic = bg;

            Image icon = UIFactory.CreateImage(go.transform, "Icon", Color.white);
            icon.sprite = SpriteFactory.Shape("triangle");
            icon.rectTransform.sizeDelta = Vector2.one * DirectionButton * 0.5f;
            icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            go.GetComponent<Button>().onClick.AddListener(() => Step(direction));
        }

        private void OnSwipe(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) Step(delta.x > 0 ? Wall.East : Wall.West);
            else Step(delta.y > 0 ? Wall.North : Wall.South);
        }

        private void Step(Wall direction)
        {
            if (InputBlocked || moving != null) return;
            MoveResult result = nav.Move(direction);
            if (result == MoveResult.Blocked)
            {
                Play(Sfx.Tap);
                if (Effects.Instance != null) Effects.Instance.Shake(player);
                if (Ctx.Hints && nav.Bumps >= 6) Mistakes = nav.Mistakes;
                return;
            }
            moving = StartCoroutine(Glide(result == MoveResult.Goal));
        }

        private IEnumerator Glide(bool reachedGoal)
        {
            Vector2 from = player.anchoredPosition;
            Vector2 to = CellCenter(nav.X, nav.Y);
            float t = 0f;
            while (t < MoveSeconds)
            {
                t += Time.unscaledDeltaTime;
                player.anchoredPosition = Vector2.Lerp(from, to, t / MoveSeconds);
                yield return null;
            }
            player.anchoredPosition = to;
            Play(Sfx.Whoosh);
            moving = null;
            if (reachedGoal)
            {
                Play(Sfx.Correct);
                Mistakes = nav.Mistakes;
                Finish();
            }
        }
    }
}
