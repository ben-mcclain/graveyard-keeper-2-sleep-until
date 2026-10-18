using System;
using System.Collections.Generic;
using LazyBearTechnology;
using UnityEngine;

namespace SleepUntil
{
    public sealed class SleepMenuData : LazyWidgetDataBase
    {
        public string CurrentTime;
        public string[] Labels;
        public Action<int> Choose;
        public Action Cancel;
    }

    // A real modal in GK2's window stack (pause/input handled by the game), with a
    // self-contained IMGUI list. No game assets, prefabs or existing dialogs modified.
    public sealed class SleepMenu : LazyWindow<SleepMenuData>
    {
        private int selected;
        private int openedFrame;
        private GUIStyle heading, text, button;
        private bool resolved;

        public override void Open(SleepMenuData value)
        {
            selected = 0;
            resolved = false;
            openedFrame = Time.frameCount;
            base.Open(value);
        }

        protected override void PrintTips() { }
        protected override void TestDraw() { }
        // No static stack-event subscriptions: topness is checked directly below.
        protected override void SubscribePermanentEvents() { }
        protected override void Update()
        {
            if (IsShownAndTop && Time.frameCount > openedFrame + 1) base.Update();
        }
        protected override bool OnPressedBack() { Cancel(); return true; }
        protected override Dictionary<GameKey, Func<bool>> GetGameKeyDelegates()
        {
            return new Dictionary<GameKey, Func<bool>>
            {
                { GameKey.Back, () => { Cancel(); return true; } },
                { GameKey.Up, () => Move(-1) },
                { GameKey.Down, () => Move(1) },
                { GameKey.DpadUp, () => Move(-1) },
                { GameKey.DpadDown, () => Move(1) },
                { GameKey.Left, () => true },
                { GameKey.Right, () => true },
                { GameKey.DpadLeft, () => true },
                { GameKey.DpadRight, () => true },
                { GameKey.Select, () => { Choose(selected); return true; } }
            };
        }

        private bool Move(int delta)
        {
            if (Time.frameCount <= openedFrame + 1 || data == null) return true;
            selected = (selected + delta + data.Labels.Length + 1) % (data.Labels.Length + 1);
            return true;
        }

        private void Choose(int index)
        {
            if (resolved || Time.frameCount <= openedFrame + 1) return;
            if (index >= data.Labels.Length) { Cancel(); return; }
            resolved = true;
            var callback = data.Choose;
            CloseWithoutCallback();
            callback(index);
        }

        private void Cancel()
        {
            if (resolved || Time.frameCount <= openedFrame + 1) return;
            resolved = true;
            var callback = data?.Cancel;
            CloseWithoutCallback();
            callback?.Invoke();
        }

        public void Abandon()
        {
            resolved = true;
            if (IsShown) CloseWithoutCallback();
            data = null;
        }

        private void OnGUI()
        {
            if (!IsShownAndTop || data == null) return;
            if (heading == null)
            {
                heading = new GUIStyle(GUI.skin.label) { fontSize = 25, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                text = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                button = new GUIStyle(GUI.skin.button) { fontSize = 18, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(16, 8, 4, 4) };
            }
            var oldMatrix = GUI.matrix;
            var oldDepth = GUI.depth;
            var oldColor = GUI.color;
            var oldBackground = GUI.backgroundColor;
            GUI.depth = -1000;
            float scale = Mathf.Min(Screen.width / 720f, Screen.height / 660f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            float width = Screen.width / scale, height = Screen.height / scale;
            GUI.color = new Color(0, 0, 0, .75f);
            GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            Rect panel = new Rect((width - 620) / 2, (height - 610) / 2, 620, 610);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 20, panel.y + 12, 580, 40), "How long would you like to sleep?", heading);
            GUI.Label(new Rect(panel.x + 20, panel.y + 54, 580, 32), data.CurrentTime, text);
            int clicked = -1;
            for (int i = 0; i <= data.Labels.Length; i++)
            {
                GUI.backgroundColor = i == selected ? new Color(.55f, .85f, .65f) : oldBackground;
                var row = new Rect(panel.x + 24, panel.y + 96 + i * 48, 572, 42);
                string label = i == data.Labels.Length ? "Cancel" : data.Labels[i];
                if (GUI.Button(row, (i == selected ? ">  " : "    ") + label, button)) clicked = i;
            }
            GUI.backgroundColor = oldBackground;
            GUI.Label(new Rect(panel.x + 22, panel.y + 537, 576, 56),
                "Morning is 6 a.m. The world keeps running during sleep.\nUse the mouse, arrows or controller to choose. Escape / Back cancels.", text);
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
            GUI.depth = oldDepth;
            if (clicked >= 0) Choose(clicked);
        }
    }
}
