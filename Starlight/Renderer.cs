using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using ClickableTransparentOverlay;
using ImGuiNET;

namespace Starlight
{
    internal class Renderer: Overlay
    {
        public int fov = 60;

        public bool enableFOV = true;
        public bool enableAntiFlash = true;
        public bool enableRadarhack = true;
        public bool aimbot = true;
        public bool aimOnTeam = false;
        public bool aimOnlyOnSpotted = false;
        public bool enableESP = true;
        public bool enableESPHealthBar = true;
        public bool enableESPHealth = true;
        public bool enableName = true;
        public bool enableESPLine = true;
        public Vector4 enemyColor = new Vector4(1, 0, 0, 1);
        public Vector4 teamColor = new Vector4(0, 1, 0, 1);
        public Vector2 screenSize = new Vector2(1920, 1080);
        public float aimbotFov = 50;
        public Vector4 circleColor = new Vector4(1, 1, 1, 1);
        public Vector4 nameColor = new Vector4(1, 1, 1, 1);
        private ConcurrentQueue<Entity> entities = new ConcurrentQueue<Entity>();
        private Entity localPlayer = new Entity();
        private readonly object entityLock = new object();

        ImDrawListPtr drawList;


        protected override void Render()
        {
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new System.Numerics.Vector4(0, 0, 0, 1)); // RGBA

            ImGui.Begin("Starlight");
            ImGui.Checkbox("Enable FOV", ref enableFOV);
            ImGui.SliderInt("FOV", ref fov, 58, 140);

            ImGui.Checkbox("Enable AntiFlash", ref enableAntiFlash);
            ImGui.Checkbox("Enable Radarhack", ref enableRadarhack);

            ImGui.Checkbox("Enable Aim Only On Spotted", ref aimOnlyOnSpotted);

            ImGui.Checkbox("Enable ESP", ref enableESP);
            ImGui.Checkbox("Enable ESP HealthBar", ref enableESPHealthBar);
            ImGui.Checkbox("Enable ESP Health", ref enableESPHealth);
            ImGui.Checkbox("Enable ESP Name", ref enableName);
            ImGui.Checkbox("Enable ESP line", ref enableESPLine);

            if (ImGui.CollapsingHeader("Team color"))
                ImGui.ColorPicker4("##teamcolor", ref teamColor);

            if (ImGui.CollapsingHeader("Enemy color"))
                ImGui.ColorPicker4("##enemycolor", ref enemyColor);

            ImGui.Checkbox("Enable Aimbot", ref aimbot);
            ImGui.Checkbox("Enable aimOnTeam", ref aimOnTeam);
            ImGui.SliderFloat("pixel FOV aimbot", ref aimbotFov, 10, 300);
            if (ImGui.CollapsingHeader("FOV circle color"))
                ImGui.ColorPicker4("##circlecolor", ref circleColor);

            DrawOvrerlay(screenSize);
            drawList = ImGui.GetWindowDrawList();
            drawList.AddCircle(new Vector2(screenSize.X / 2, screenSize.Y / 2), aimbotFov, ImGui.ColorConvertFloat4ToU32(circleColor));

            if (enableESP)
            {
                foreach (var entity in entities)
                {
                    if (EntityOnScreen(entity))
                    {
                        DrawBox(entity);
                        if (enableESPHealthBar)
                        {
                            DrawHealthBar(entity);
                        }
                        if (enableESPLine)
                        {
                            DrawLine(entity);
                        } 
                        if (enableESPHealth)
                        {
                            DrawHealth(entity);
                        }
                        if (enableName)
                        {
                            DrawName(entity, 20);
                        }
                    }
                }
            }
        }

        bool EntityOnScreen(Entity entity)
        {
            if (entity.position2D.X > 0 && entity.position2D.X < screenSize.X && entity.position2D.Y > 0 && entity.position2D.Y < screenSize.Y);
            {
                return true;
            }
            return false;
        }

        private void DrawName(Entity entity, int yOffset)
        {
            Vector2 textLocation = new Vector2(entity.viewPosition2D.X, entity.viewPosition2D.Y - yOffset);
            drawList.AddText(textLocation, ImGui.ColorConvertFloat4ToU32(nameColor), $"{entity.name}");
        }

        private void DrawHealth(Entity entity)
        {
            float entityHeight = entity.position2D.Y - entity.viewPosition2D.Y;
            float barHeight = entityHeight * (entity.health / 100f);
            string healthText = $"{entity.health}% HP";


            Vector2 textSize = ImGui.CalcTextSize(healthText);
            Vector2 textPosition = new Vector2(entity.viewPosition2D.X - textSize.X / 2, entity.viewPosition2D.Y - 15); // Центрируем текст над полосой здоровья

            ImGui.SetCursorPos(textPosition);
            ImGui.TextColored(new Vector4(1, 1, 1, 1), healthText); // Белый цвет текста
        }

        private void DrawHealthBar(Entity entity)
        {
            float entityHeight = entity.position2D.Y - entity.viewPosition2D.Y;
            float boxLeft = entity.viewPosition2D.X - entityHeight / 3;
            float boxRight = entity.position2D.X + entityHeight / 3;
            float barPercentWidth = 0.05f;
            float barPixelWidth = barPercentWidth * (boxRight - boxLeft);
            float barHeight = entityHeight * (entity.health / 100f);
            string healthText = $"{entity.health}% HP";


            Vector2 barTop = new Vector2(boxLeft - barPixelWidth, entity.position2D.Y - barHeight);
            Vector2 barBottom = new Vector2(boxLeft, entity.position2D.Y);

            Vector4 barColor = new Vector4(0, 1, 0, 1);

            drawList.AddRectFilled(barTop, barBottom, ImGui.ColorConvertFloat4ToU32(barColor));

            Vector2 textSize = ImGui.CalcTextSize(healthText);
            Vector2 textPosition = new Vector2(entity.viewPosition2D.X - textSize.X / 2, entity.viewPosition2D.Y - 15); // Центрируем текст над полосой здоровья

            ImGui.SetCursorPos(textPosition);
            ImGui.TextColored(new Vector4(1, 1, 1, 1), healthText); // Белый цвет текста
        }

        private void DrawBox(Entity entity)
        {
            float entityHeight = entity.position2D.Y - entity.viewPosition2D.Y;

            Vector2 rectTop = new Vector2(entity.viewPosition2D.X - entityHeight / 3, entity.viewPosition2D.Y);
            Vector2 rectBottom = new Vector2(entity.position2D.X + entityHeight / 3, entity.position2D.Y);

            Vector4 boxColor = localPlayer.team == entity.team ? teamColor : enemyColor;

            drawList.AddRect(rectTop, rectBottom, ImGui.ColorConvertFloat4ToU32(boxColor));
        }

        private void DrawLine(Entity entity)
        {
            Vector4 lineColor = localPlayer.team == entity.team ? teamColor : enemyColor;

            drawList.AddLine(new Vector2(screenSize.X / 2, screenSize.Y), entity.position2D, ImGui.ColorConvertFloat4ToU32(lineColor));
        }

        public void UpdateEntities(IEnumerable<Entity> newEntities)
        {
            entities = new ConcurrentQueue<Entity>(newEntities);
        }

        public void UpdateLocalPlayer(Entity newEntity)
        {
            lock (entityLock)
            {
                localPlayer = newEntity;
            }
        }

        public Entity GetLocalPlayer()
        {
            lock (entityLock)
            {
            return localPlayer;
            }
        }

        void DrawOvrerlay(Vector2 screenSize)
        {
            ImGui.SetNextWindowSize(screenSize);
            ImGui.SetNextWindowPos(new Vector2(0, 0));
            ImGui.Begin("overlay", ImGuiWindowFlags.NoDecoration
                | ImGuiWindowFlags.NoBackground
                | ImGuiWindowFlags.NoBringToFrontOnFocus
                | ImGuiWindowFlags.NoMove
                | ImGuiWindowFlags.NoInputs
                | ImGuiWindowFlags.NoCollapse
                | ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoScrollWithMouse
                );
        }
    }
}
