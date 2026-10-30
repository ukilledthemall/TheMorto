using UnityEngine;

namespace TheMorto
{
    /// <summary>Fixed 2D room views, external camera tablet, menu and input.</summary>
    [RequireComponent(typeof(MortoController))]
    public sealed class MortoView : MonoBehaviour
    {
        public Texture2D living, hallway, kitchen, fence, sideyard, porch, morto, portrait, scare;
        public Texture2D mortoLiving, mortoHallway, mortoKitchen, mortoFence, mortoSideyard, mortoPorch;
        MortoController controller;
        GUIStyle small, normal, big, title, menuChoice, button, center;
        Texture2D white, grain, vignette, lightMask;
        Color[] darknessPixels = new Color[256 * 144];
        readonly bool[] held = new bool[512];
        bool mouseLight, showInstructions;
        Vector2 pointer = new Vector2(960, 490);
        float pan;
        readonly Color bone = new Color(.83f, .83f, .77f);
        readonly Color muted = new Color(.43f, .48f, .45f);
        readonly Color amber = new Color(.77f, .62f, .36f);
        static readonly string[] RoomNames = { "LIVING ROOM", "HALLWAY", "KITCHEN" };
        static readonly string[] CameraNames = { "BACK GARDEN", "SIDE PATH", "FRONT PORCH" };
        static readonly string[] CoverNames = { "BEHIND THE SOFA", "IN THE COAT CLOSET", "BEHIND THE COUNTER" };

        void Awake() { controller = GetComponent<MortoController>(); }
        void Update()
        {
            if (controller.Game == null) return;
            controller.Flashlight(held[(int)KeyCode.F] || mouseLight);
            pan = Mathf.Lerp(pan, (pointer.x - 960) / 960, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 5));
        }
        void OnGUI()
        {
            if (controller.Game == null) return;
            Initialize();
            Matrix4x4 original = GUI.matrix;
            Vector2 screenPointer = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1920 * scale) / 2,
                (Screen.height - 1080 * scale) / 2, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            pointer = GUIUtility.ScreenToGUIPoint(screenPointer);
            HandleInput();
            Box(new Rect(0, 0, 1920, 1080), Color.black);
            Draw();
            GUI.matrix = original;
        }
        void Initialize()
        {
            if (normal != null) return;
            normal = new GUIStyle(GUI.skin.label) { fontSize = 24, wordWrap = true };
            Font mono = Font.CreateDynamicFontFromOSFont("Courier New", 24);
            if (mono != null) normal.font = mono;
            small = new GUIStyle(normal) { fontSize = 18 };
            big = new GUIStyle(normal) { fontSize = 46 };
            title = new GUIStyle(normal) { fontSize = 112, fontStyle = FontStyle.Normal };
            menuChoice = new GUIStyle(normal) { fontSize = 40, alignment = TextAnchor.MiddleLeft };
            button = new GUIStyle(GUI.skin.button) { fontSize = 21, wordWrap = true, font = normal.font };
            center = new GUIStyle(normal) { alignment = TextAnchor.MiddleCenter };
            white = Texture2D.whiteTexture;
            grain = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            grain.wrapMode = TextureWrapMode.Repeat; grain.filterMode = FilterMode.Point;
            var noise = new Color[128 * 128]; var rng = new System.Random(381);
            for (int i = 0; i < noise.Length; i++) noise[i] = new Color(1, 1, 1, (float)rng.NextDouble() * .075f);
            grain.SetPixels(noise); grain.Apply();
            vignette = new Texture2D(256, 144, TextureFormat.RGBA32, false);
            for (int y = 0; y < 144; y++) for (int x = 0; x < 256; x++)
            {
                float nx = (x - 127.5f) / 128, ny = (y - 71.5f) / 72;
                vignette.SetPixel(x, y, new Color(0, 0, 0, Mathf.Clamp01((nx * nx + ny * ny - .12f) * .65f)));
            }
            vignette.Apply();
            lightMask = new Texture2D(256, 144, TextureFormat.RGBA32, false);
            lightMask.wrapMode = TextureWrapMode.Clamp;
            lightMask.filterMode = FilterMode.Bilinear;
        }

        void Box(Rect rect, Color color)
        {
            Color old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, white); GUI.color = old;
        }
        void Text(Rect rect, string value, GUIStyle style = null, Color? color = null)
        {
            Color old = GUI.color; GUI.color = color ?? bone;
            GUI.Label(rect, value, style ?? normal); GUI.color = old;
        }
        void Image(Rect rect, Texture2D image, Color color, bool fit = false)
        {
            if (image == null) return;
            Color old = GUI.color; GUI.color = color;
            GUI.DrawTexture(rect, image, fit ? ScaleMode.ScaleToFit : ScaleMode.ScaleAndCrop, true);
            GUI.color = old;
        }
        bool Button(Rect rect, string value, bool active = false)
        {
            Box(rect, active ? new Color(.17f, .18f, .14f, .97f) : new Color(.014f, .023f, .02f, .92f));
            Box(new Rect(rect.x, rect.y, 2, rect.height), active ? amber : muted);
            Color old = GUI.backgroundColor; GUI.backgroundColor = new Color(.08f, .11f, .09f, .6f);
            bool clicked = GUI.Button(rect, value, button); GUI.backgroundColor = old; return clicked;
        }
        void Grain(float opacity)
        {
            Color old = GUI.color; GUI.color = new Color(1, 1, 1, opacity);
            float shift = Mathf.Floor(Time.realtimeSinceStartup * 8) * .19f;
            GUI.DrawTextureWithTexCoords(new Rect(0, 0, 1920, 1080), grain, new Rect(shift, shift, 15, 9)); GUI.color = old;
            for (int y = 0; y < 1080; y += 8) Box(new Rect(0, y, 1920, 1), new Color(0, 0, 0, .08f));
        }
        void Draw()
        {
            NightGame g = controller.Game;
            if (g.Phase == NightPhase.Menu) { Menu(); return; }
            if (g.Phase == NightPhase.Lost) { Defeat(g); return; }
            if (g.Phase == NightPhase.Won) { Dawn(g); return; }
            if (g.MonitorOpen) CameraView(g); else RoomView(g);
            if (g.MonitorOpen) Grain(.3f);
            Image(new Rect(0, 0, 1920, 1080), vignette, Color.white);
            HUD(g);
            if (g.Phase == NightPhase.Paused) Pause();
        }
        void Menu()
        {
            // Familiar static-filled horror-menu composition, using only the user's artwork.
            Image(new Rect(755, -70, 1270, 1260), portrait, new Color(.57f, .54f, .43f));
            Box(new Rect(0, 0, 830, 1080), new Color(0, 0, 0, .95f));
            Box(new Rect(830, 0, 240, 1080), new Color(0, 0, 0, .35f));
            Text(new Rect(150, 110, 780, 340), "THE\nMORTO", title);
            Text(new Rect(158, 452, 660, 60), "HE HAS COME BACK.", small, muted);
            if (MenuItem(new Rect(150, 565, 640, 74), "New Game")) { showInstructions = true; }
            string progress = controller.FinishedAll ? "Continue  /  Night 3" : "Continue  /  Night " + controller.UnlockedNight;
            if (MenuItem(new Rect(150, 650, 660, 74), progress)) controller.ContinueGame();
            if (controller.CompletedNights > 0) Text(new Rect(150, 724, 640, 45), new string('★', controller.CompletedNights), normal, amber);
            if (MenuItem(new Rect(150, 790, 640, 64), "How to survive", true)) showInstructions = true;
            Text(new Rect(62, 1010, 900, 40), "v1.0    A HOUSE AT THE EDGE OF TOWN", small, muted);
            if (Button(new Rect(1580, 980, 280, 52), controller.Muted ? "SOUND OFF [M]" : "SOUND ON [M]")) controller.ToggleMute();
            Grain(.75f); Image(new Rect(0, 0, 1920, 1080), vignette, Color.white);
            if (showInstructions) Instructions();
        }
        bool MenuItem(Rect rect, string value, bool secondary = false)
        {
            bool over = rect.Contains(pointer);
            Text(new Rect(rect.x - 56, rect.y + 8, 60, rect.height), over ? ">>" : "", menuChoice);
            GUIStyle style = secondary ? new GUIStyle(small) : menuChoice;
            Color old = GUI.color; GUI.color = over ? Color.white : bone;
            bool click = GUI.Button(rect, value, style); GUI.color = old; return click;
        }
        void Instructions()
        {
            Box(new Rect(0, 0, 1920, 1080), new Color(0, 0, 0, .96f));
            Text(new Rect(250, 115, 1430, 80), "BEFORE THE HOUSE GOES QUIET", big);
            Text(new Rect(255, 235, 1380, 350),
                "Listen for hollow hoofsteps. Hooves mean he is inside, but not which room. Light reveals him and attracts him.\nAwareness rises with light and fades in darkness. Hiding or moving lowers it faster.\n\nMove between the living room, hall, and kitchen. Stay hidden while he searches for you.\n\nCheck the outside cameras. The house has no electricity. Cameras run on a separate backup battery. Use the speaker [L] on an outdoor feed to draw him there. Watch him on the camera to make him hesitate. You are vulnerable with the monitor open.", normal);
            Text(new Rect(255, 690, 1370, 110), "Aim mouse + F: flashlight     SPACE: cameras     1–3: feeds\nA / D: adjacent room     H: hide / leave cover     P / ESC: pause\nM: mute     R: retry after a loss", normal, amber);
            if (Button(new Rect(255, 875, 520, 75), "BEGIN NEW GAME")) { showInstructions = false; controller.NewGame(); }
            if (Button(new Rect(815, 875, 350, 75), "BACK")) showInstructions = false;
        }
        Texture2D RoomImage(int room) { return room == 0 ? living : room == 1 ? hallway : kitchen; }
        void RoomView(NightGame g)
        {
            Image(new Rect(0, 0, 1920, 1080), RoomImage(g.PlayerRoom), Color.white);
            if (g.SameRoom && !g.Hiding)
                Image(MortoPose(g.PlayerRoom, false), MortoTexture(g.PlayerRoom, false), new Color(.72f, .72f, .68f));
            DrawDarkness(pointer.x, pointer.y, 432, g.FlashOn);
            if (g.Travelling)
            {
                Box(new Rect(0, 0, 1920, 1080), new Color(0, 0, 0, .85f));
                Text(new Rect(500, 480, 920, 80), "MOVING TO " + RoomNames[g.Destination], center, muted);
                return;
            }
            if (g.Hiding)
            {
                // Narrow view from cover; its finite protection remains clear in the HUD.
                Box(new Rect(0, 0, 1920, 390), new Color(0, 0, 0, .97f));
                Box(new Rect(0, 660, 1920, 420), new Color(0, 0, 0, .97f));
                Box(new Rect(0, 380, 190, 300), new Color(0, 0, 0, .93f));
                Box(new Rect(1730, 380, 190, 300), new Color(0, 0, 0, .93f));
                Text(new Rect(550, 692, 820, 55), CoverNames[g.PlayerRoom], center, muted);
            }
            bool old = GUI.enabled; GUI.enabled = g.Phase == NightPhase.Playing;
            if (g.PlayerRoom > 0)
            {
                GUI.enabled = old && g.Phase == NightPhase.Playing && g.MoveCooldown <= 0;
                if (Button(new Rect(576, 907, 365, 59), "‹  " + RoomNames[g.PlayerRoom - 1] + "  [A]")) controller.MoveTo(g.PlayerRoom - 1);
            }
            if (g.PlayerRoom < 2)
            {
                GUI.enabled = old && g.Phase == NightPhase.Playing && g.MoveCooldown <= 0;
                if (Button(new Rect(963, 907, 365, 59), RoomNames[g.PlayerRoom + 1] + "  [D]  ›")) controller.MoveTo(g.PlayerRoom + 1);
            }
            GUI.enabled = g.Phase == NightPhase.Playing;
            if (Button(new Rect(710, 966, 500, 67), "RAISE CAMERAS  [ SPACE ]")) controller.ToggleMonitor();
            string hideLabel = g.Hiding ? "LEAVE COVER  [H]" : g.HideCooldown > 0 ? "COVER READY IN " + g.HideCooldown.ToString("0.0") + "s" : "HIDE  [H]";
            GUI.enabled = g.Phase == NightPhase.Playing && (g.Hiding || g.HideCooldown <= 0 && g.HideRoomUsed != g.PlayerRoom);
            if (Button(new Rect(1478, 907, 384, 59), hideLabel, g.Hiding)) controller.Hide();
            GUI.enabled = g.Phase == NightPhase.Playing && !g.Hiding && g.FlashBattery > 0;
            Rect light = new Rect(1478, 966, 384, 67);
            if (GUI.RepeatButton(light, g.FlashOn ? "LIGHT ON  /  RELEASE" : "HOLD LIGHT  [F]", button)) mouseLight = true;
            GUI.enabled = old;
        }
        Texture2D MortoTexture(int index, bool outside)
        {
            Texture2D[] variants = outside ? new[] { mortoFence, mortoSideyard, mortoPorch } : new[] { mortoLiving, mortoHallway, mortoKitchen };
            return variants[index] != null ? variants[index] : morto;
        }
        Rect MortoPose(int index, bool outside)
        {
            Vector3[] rooms = {new Vector3(1185,130,710),new Vector3(1015,170,680),new Vector3(835,100,745)};
            Vector3[] feeds = {new Vector3(975,290,440),new Vector3(775,235,390),new Vector3(610,130,690)};
            Vector3 p = (outside ? feeds : rooms)[index] * 1.2f;
            Texture2D sprite = MortoTexture(index, outside);
            float width = sprite != null ? p.z * sprite.width / sprite.height : p.z * .4f;
            return new Rect(p.x - width / 2, p.y, width, p.z);
        }
        void DrawDarkness(float cx, float cy, float radius, bool reveal)
        {
            if (Event.current.type == EventType.Repaint)
            {
                for (int y=0; y<144; y++) for (int x=0; x<256; x++)
                {
                    float dx=(x+.5f)/256*1920-cx, dy=(143-y+.5f)/144*1080-cy;
                    float r=Mathf.Sqrt(dx*dx+dy*dy)/radius;
                    float alpha=reveal ? Mathf.Clamp01((r-.38f)/.62f) : 1;
                    darknessPixels[y*256+x]=new Color(0,0,0,alpha);
                }
                lightMask.SetPixels(darknessPixels); lightMask.Apply(false);
            }
            Image(new Rect(0,0,1920,1080), lightMask, Color.white);
        }
        void CameraView(NightGame g)
        {
            Texture2D[] feeds = { fence, sideyard, porch };
            Image(new Rect(0, 0, 1920, 1080), feeds[g.SelectedCamera], Color.white);
            if (g.MonsterNode == g.SelectedCamera)
            {
                Image(MortoPose(g.SelectedCamera, true), MortoTexture(g.SelectedCamera, true), new Color(.48f,.48f,.45f));
            }
            DrawDarkness(960, 540, 708, true);

            Box(new Rect(650, 150, 630, 110), new Color(0, 0, 0, .7f));
            Text(new Rect(670, 169, 590, 80), "CAM 0" + (g.SelectedCamera + 1) + "\n" + CameraNames[g.SelectedCamera], normal);
            Text(new Rect(1700, 165, 160, 45), "● REC", small, muted);
            Box(new Rect(1344, 500, 518, 300), new Color(.005f, .012f, .008f, .91f));
            Text(new Rect(1368, 515, 470, 35), "EXTERIOR SURVEILLANCE", small, muted);
            for (int i = 0; i < 3; i++)
                if (Button(new Rect(1368, 565 + i * 57, 470, 48), "0" + (i + 1) + "  " + CameraNames[i], g.SelectedCamera == i)) controller.SelectCamera(i);
            Text(new Rect(1368, 744, 470, 55), g.CameraHint + (g.WatchDelay > 0 ? " / HESITATING" : ""), small, amber);
            if (Button(new Rect(710, 966, 500, 67), "LOWER CAMERAS  [ SPACE ]")) controller.ToggleMonitor();
            bool enabledBeforeLure=GUI.enabled;
            GUI.enabled=g.CanAct&&g.MonsterNode<3&&g.LureCooldown<=0&&g.MonitorBattery>=controller.tuning.lureCost;
            if(Button(new Rect(1344, 860, 518, 55),g.LureCooldown>0?"SPEAKER READY IN "+Mathf.CeilToInt(g.LureCooldown)+"s":"PLAY SPEAKER [L] · "+controller.tuning.lureCost+"%"))controller.Lure();
            GUI.enabled=enabledBeforeLure;
            if (controller.CameraGlitch > 0) Box(new Rect(0, 0, 1920, 950), new Color(.38f, .43f, .38f, controller.CameraGlitch));
        }
        void HUD(NightGame g)
        {
            Text(new Rect(58, 40, 650, 40), "THE MORTO", normal);
            Text(new Rect(58, 83, 650, 32), g.MonitorOpen ? "CAM 0" + (g.SelectedCamera+1) + " / " + CameraNames[g.SelectedCamera] : RoomNames[g.PlayerRoom], small, muted);
            int hour=Mathf.Min(6,Mathf.FloorToInt(g.Elapsed/g.Duration*6));
            Text(new Rect(1650,30,230,65),(hour==0?"12":hour.ToString())+" AM",big);
            Text(new Rect(1700,95,180,32),"Night "+g.Night,small,muted);
            if(Button(new Rect(864,43,145,43),"PAUSE · P"))controller.Pause();
            if(Button(new Rect(1056,43,145,43),controller.Muted?"SOUND OFF":"SOUND ON"))controller.ToggleMute();
            Text(new Rect(58,912,365,35),"FLASHLIGHT "+Mathf.CeilToInt(g.FlashBattery)+"%",small);
            Box(new Rect(58,952,365,5),new Color(.16f,.18f,.16f));Box(new Rect(58,952,365*g.FlashBattery/100,5),bone);
            Text(new Rect(58,970,365,32),"ATTENTION "+Mathf.CeilToInt(g.Awareness)+"%",small,amber);
            Box(new Rect(58,1010,365*Mathf.Clamp01(g.Awareness/100),4),amber);
            if(g.MonitorOpen)Text(new Rect(1368,810,470,35),"BACKUP "+Mathf.CeilToInt(g.MonitorBattery)+"%",small,muted);
            if(g.Hiding)Text(new Rect(58,180,1500,45),"HIDING · HOLDING BREATH · "+Mathf.CeilToInt(g.HideSafeRemaining)+"s",normal,amber);
            else if(g.HideCooldown>0)Text(new Rect(58,180,600,45),"CATCHING BREATH · "+Mathf.CeilToInt(g.HideCooldown)+"s",small,muted);
            if(g.NoticeRemaining>0)Text(new Rect(515,810,890,64),g.Notice,center);
            Text(new Rect(300,1040,1400,35),"A/D MOVE · AIM MOUSE + HOLD F LIGHT · H HIDE · SPACE CAMERAS · 1–3 FEEDS · P PAUSE",center,muted);
        }
        void Pause()
        {
            Box(new Rect(0, 0, 1920, 1080), new Color(0, 0, 0, .94f));
            Text(new Rect(650, 280, 900, 100), "PAUSED", title);
            Text(new Rect(660, 455, 800, 90), "The house waits. All timers are frozen.", normal);
            if (Button(new Rect(660, 620, 600, 75), "RESUME  [P / ESC]")) controller.Pause();
            if (Button(new Rect(660, 720, 600, 75), "MAIN MENU")) controller.Menu();
        }
        void Defeat(NightGame g)
        {
            float t = controller.EndingAge;
            if (t < controller.ScareSeconds)
            {
                // Full-window layered composition, preserving the portrait skull proportions.
                Matrix4x4 previousMatrix = GUI.matrix;
                Color previousColor = GUI.color;
                GUI.matrix = Matrix4x4.identity;
                GUI.color = new Color(.3f,.3f,.3f);
                GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),scare,ScaleMode.ScaleAndCrop,true);
                GUI.color = Color.white;
                float scale = t < .10f ? Mathf.Lerp(.96f,1.07f,t/.10f) : Mathf.Lerp(1.07f,1.03f,Mathf.Clamp01((t-.10f)/.08f));
                float height=Screen.height*scale;
                float offset=Mathf.Sin(t*27)*Screen.width*.002f;
                GUI.DrawTexture(new Rect(offset,(Screen.height-height)/2,Screen.width,height),scare,ScaleMode.ScaleAndCrop,true);
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
                Grain(.65f); return;
            }
            Image(new Rect(980, 0, 940, 1080), portrait, new Color(.22f, .19f, .15f));
            Text(new Rect(170, 260, 1450, 150), "YOU WERE FOUND", title);
            Text(new Rect(180, 450, 1190, 100), g.LossReason, big);
            Text(new Rect(180, 600, 1190, 100), "Light only reveals him and attracts him. Change rooms or hide to lower awareness.\nChange rooms before the hoofsteps stop beside you.", normal, muted);
            if (Button(new Rect(180, 790, 540, 75), "RETRY NIGHT " + g.Night + "  [R]")) controller.Retry();
            if (Button(new Rect(755, 790, 350, 75), "MAIN MENU")) controller.Menu();
        }
        void Dawn(NightGame g)
        {
            Image(new Rect(0, 0, 1920, 1080), living, new Color(.31f, .32f, .29f));
            Box(new Rect(0, 0, 1920, 1080), new Color(0, 0, 0, .62f));
            Text(new Rect(200, 200, 1400, 170), "6 AM", title);
            Text(new Rect(210, 420, 1450, 100), g.Night < 3 ? "HE WILL RETURN TOMORROW." : "THREE MORNINGS. STILL NO ANSWERS.", big);
            Text(new Rect(210, 555, 1400, 100), "Night " + g.Night + " survived.  Flashlight: " + Mathf.CeilToInt(g.FlashBattery) + "%  /  Searches escaped: " + g.Escapes, normal, muted);
            if (g.Night < 3 && Button(new Rect(210, 740, 580, 75), "CONTINUE TO NIGHT " + (g.Night + 1))) controller.NextNight();
            if (Button(new Rect(830, 740, 400, 75), "MAIN MENU")) controller.Menu();
            if (g.Night == 3) Text(new Rect(210, 890, 1450, 70), "The gate was closed when you checked. It is open again.", normal, amber);
        }
        void HandleInput()
        {
            Event e = Event.current;
            if (e.type == EventType.MouseUp) mouseLight = false;
            if (e.type != EventType.KeyDown && e.type != EventType.KeyUp) return;
            int key = (int)e.keyCode; if (key < 0 || key >= held.Length) return;
            if (e.type == EventType.KeyUp) { held[key] = false; return; }
            if (held[key]) { e.Use(); return; }
            held[key] = true;
            NightGame g = controller.Game;
            switch (e.keyCode)
            {
                case KeyCode.L: controller.Lure(); break;
                case KeyCode.Space: controller.ToggleMonitor(); break;
                case KeyCode.Alpha1: controller.SelectCamera(0); break;
                case KeyCode.Alpha2: controller.SelectCamera(1); break;
                case KeyCode.Alpha3: controller.SelectCamera(2); break;
                case KeyCode.A: case KeyCode.LeftArrow: controller.MoveTo(g.PlayerRoom - 1); break;
                case KeyCode.D: case KeyCode.RightArrow: controller.MoveTo(g.PlayerRoom + 1); break;
                case KeyCode.H: controller.Hide(); break;
                case KeyCode.P: case KeyCode.Escape: controller.Pause(); break;
                case KeyCode.M: controller.ToggleMute(); break;
                case KeyCode.R: if (g.Phase == NightPhase.Lost) controller.Retry(); break;
            }
            e.Use();
        }
        void OnApplicationFocus(bool focused)
        {
            if (!focused) { System.Array.Clear(held, 0, held.Length); mouseLight = false; controller.Flashlight(false); }
        }
        void OnDisable()
        {
            System.Array.Clear(held, 0, held.Length); mouseLight = false;
            if (controller != null && controller.Game != null) controller.Flashlight(false);
        }
        void OnDestroy()
        {
            foreach (Texture2D texture in new[] { grain, vignette, lightMask }) if (texture != null) Destroy(texture);
        }
    }
}
