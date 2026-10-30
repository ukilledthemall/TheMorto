using UnityEngine;
using UnityEngine.Events;

namespace TheMorto
{
    /// <summary>Game clock, persistence and event-driven audio for the flat-image view.</summary>
    [DisallowMultipleComponent]
    public sealed class MortoController : MonoBehaviour
    {
        public NightTuning tuning = new NightTuning();
        public AudioClip hum, hoof, cameraSound, flashSound, moveSound, scare, win, knock, breathe;
        [Tooltip("Optional title-screen music. Drop your MP3 here; it loops in the menu only.")]
        public AudioClip menuMusic;
        [Range(0f, 1f)] public float volume = .65f;
        public UnityEvent onNightWon = new UnityEvent();
        public UnityEvent onCaught = new UnityEvent();
        public NightGame Game { get; private set; }
        public bool Muted { get; private set; }
        public int UnlockedNight { get; private set; } = 1;
        public float EndingAge { get; private set; }
        public float CameraGlitch { get; private set; }
        public float ScareSeconds { get { return scare != null ? Mathf.Max(1.1f, scare.length) : 1.1f; } }
        public bool FinishedAll { get; private set; }
        public int CompletedNights { get; private set; }

        const string ProgressKey = "TheMorto.UnlockedNight";
        const string FinishedKey = "TheMorto.Completed";
        AudioSource ambience, effects, danger, menuAudio;
        AudioClip lureTone;
        bool wasLit;

        void Awake()
        {
            float[] tone = new float[11025];
            for(int i=0;i<tone.Length;i++) { float t=i/22050f;float local=t<.22f?t:t-.22f;float freq=t<.22f?620:740;tone[i]=local<.18f?Mathf.Sin(2*Mathf.PI*freq*local)*.18f*Mathf.Sin(Mathf.PI*local/.18f):0; }
            lureTone=AudioClip.Create("Camera speaker signal",tone.Length,1,22050,false);lureTone.SetData(tone,0);
            Game = new NightGame(tuning); Game.Cue += HandleCue;
            UnlockedNight = Mathf.Clamp(PlayerPrefs.GetInt(ProgressKey, 1), 1, 3);
            FinishedAll = PlayerPrefs.GetInt(FinishedKey, 0) != 0;
            CompletedNights = PlayerPrefs.GetInt("TheMorto.CompletedNights", FinishedAll ? 3 : UnlockedNight - 1);
            ambience = gameObject.AddComponent<AudioSource>();
            effects = gameObject.AddComponent<AudioSource>();
            danger = gameObject.AddComponent<AudioSource>();
            ambience.playOnAwake = effects.playOnAwake = danger.playOnAwake = false;
            ambience.spatialBlend = effects.spatialBlend = danger.spatialBlend = 0;
            ambience.loop = true; ambience.clip = hum;
            menuAudio = gameObject.AddComponent<AudioSource>();
            menuAudio.playOnAwake = false; menuAudio.spatialBlend = 0; menuAudio.loop = true;
        }
        void Update()
        {
            Game.Tick(Time.unscaledDeltaTime);
            CameraGlitch = Mathf.Max(0, CameraGlitch - Time.unscaledDeltaTime);
            if (Game.Phase == NightPhase.Won || Game.Phase == NightPhase.Lost) EndingAge += Time.unscaledDeltaTime;
            bool lit = Game.FlashOn;
            if (lit && !wasLit) Play(flashSound, .45f);
            wasLit = lit;
            bool inMenu = Game.Phase == NightPhase.Menu;
            menuAudio.volume = Muted ? 0 : volume;
            if (menuAudio.clip != menuMusic) { menuAudio.Stop(); menuAudio.clip = menuMusic; }
            if (inMenu && menuMusic != null && !menuAudio.isPlaying) menuAudio.Play();
            else if (!inMenu && menuAudio.isPlaying) menuAudio.Stop();
            bool live = Game.Phase == NightPhase.Playing;
            ambience.volume = Muted ? 0 : volume * .16f;
            if (live && !ambience.isPlaying && hum != null) ambience.Play();
            else if (!live && ambience.isPlaying) ambience.Pause();
        }
        public void NewGame()
        {
            UnlockedNight = 1; FinishedAll = false; CompletedNights = 0;
            PlayerPrefs.SetInt("TheMorto.CompletedNights", 0);
            PlayerPrefs.SetInt(ProgressKey, 1); PlayerPrefs.SetInt(FinishedKey, 0); PlayerPrefs.Save();
            StartNight(1);
        }
        public void ContinueGame() { StartNight(UnlockedNight); }
        public void StartNight(int night)
        {
            menuAudio.Stop(); effects.Stop(); danger.Stop(); EndingAge = 0; CameraGlitch = .25f; wasLit = false;
            Game.Start(Mathf.Clamp(night, 1, UnlockedNight));
        }
        public void NextNight() { if (Game.Phase == NightPhase.Won && Game.Night < 3) StartNight(Game.Night + 1); }
        public void Retry() { StartNight(Game.Night); }
        public void Menu() { Game.ReturnToMenu(); effects.Stop(); danger.Stop(); }
        public void Lure() { Game.Lure(); }
        public void ToggleMonitor() { Game.ToggleMonitor(); }
        public void SelectCamera(int index) { Game.SelectCamera(index); }
        public void Flashlight(bool held) { Game.SetFlashlight(held); }
        public void MoveTo(int room) { Game.MoveTo(room); }
        public void Hide() { Game.ToggleHide(); }
        public void Pause()
        {
            Game.TogglePause();
            if (Game.Phase == NightPhase.Paused) { effects.Stop(); danger.Stop(); }
        }
        public void ToggleMute() { Muted = !Muted; effects.mute = danger.mute = ambience.mute = menuAudio.mute = Muted; }
        void HandleCue(string cue)
        {
            float pan = Game.MonsterNode == 4 ? .7f : Game.MonsterNode == 5 ? -.7f : 0;
            switch (cue)
            {
                case "lure": Play(lureTone, .8f); break;
                case "hoof": if (Game.MonsterNode >= 3) Play(hoof, .7f, 0); break;
                case "near": break;
                
                case "escaped": effects.Stop(); break;
                case "watched": break;
                case "attracted": if (Game.MonsterNode >= 3) Play(knock, .5f, 0); break;
                case "camera": CameraGlitch = .18f; Play(cameraSound, .4f); break;
                case "move": Play(moveSound, .48f); break;
                case "hide": case "unhide": Play(breathe, .35f); break;
                case "batteryDead": case "cameraDead": Play(cameraSound, .4f); break;
                case "loss": effects.Stop(); ambience.Stop(); EndingAge = 0; Play(scare, .85f, 0, true); onCaught.Invoke(); break;
                case "win":
                    EndingAge = 0; CompletedNights = Mathf.Max(CompletedNights, Game.Night); PlayerPrefs.SetInt("TheMorto.CompletedNights", CompletedNights); UnlockedNight = Mathf.Max(UnlockedNight, Mathf.Min(3, Game.Night + 1));
                    if (Game.Night == 3) FinishedAll = true;
                    PlayerPrefs.SetInt(ProgressKey, UnlockedNight); PlayerPrefs.SetInt(FinishedKey, FinishedAll ? 1 : 0); PlayerPrefs.Save();
                    Play(win, .6f); onNightWon.Invoke(); break;
            }
        }
        void Play(AudioClip clip, float loudness, float pan = 0, bool priority = false)
        {
            if (clip == null || Muted) return;
            AudioSource source = priority ? danger : effects;
            source.panStereo = pan; source.PlayOneShot(clip, loudness * volume);
        }
        void OnApplicationFocus(bool focused)
        {
            if (!focused && Game != null) { Game.SetFlashlight(false); if (Game.Phase == NightPhase.Playing) Pause(); }
        }
        void OnApplicationPause(bool paused) { if (paused) OnApplicationFocus(false); }
        void OnDestroy() { if (Game != null) Game.Cue -= HandleCue; if(lureTone!=null)Destroy(lureTone); }
    }
}
