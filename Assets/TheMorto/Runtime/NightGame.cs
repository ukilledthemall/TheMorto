using System;
namespace TheMorto
{
    public enum NightPhase { Menu, Playing, Paused, Won, Lost }
    [Serializable] public sealed class NightTuning
    {
        public float nightOneDuration=150, nightTwoDuration=165, nightThreeDuration=180;
        public float nightOneMoveInterval=4.5f, nightTwoMoveInterval=3.8f, nightThreeMoveInterval=3.2f;
        public float nightOneAttackGrace=3.2f, nightTwoAttackGrace=2.8f, nightThreeAttackGrace=2.4f;
        public float flashlightDrain=5.5f, cameraDrain=1.8f;
        public float awarenessGain=33.6f, awarenessDecay=12, hidingAwarenessDrop=35, roomChangeAwarenessDrop=50, awarenessThreshold=100;
        public float moveDuration=1, moveCooldown=1.5f, movementAwareness=0;
        public float hideCover=15, hideCooldown=8, searchBase=6, searchNightIncrease=2, wanderDuration=12;
        public float lureCost=6, lureCooldown=10, lureHold=5, lureSuccessChance=.7f;
        public float cameraWatchExposure=1.25f, cameraHesitation=3, darkGraceMultiplier=.75f;
    }
    /// <summary>Same v1.3 rules as Browser/model.js. Nodes: fence, side, porch, hall, kitchen, living.</summary>
    public sealed class NightGame
    {
        public NightPhase Phase {get;private set;}=NightPhase.Menu;
        public int Night {get;private set;}=1;
        public float Duration {get;private set;}=150;
        public float Elapsed {get;private set;}
        public int PlayerRoom {get;private set;}
        public int Destination {get;private set;}=-1;
        public float TravelRemaining {get;private set;}
        public float MoveCooldown {get;private set;}
        public bool Travelling {get{return TravelRemaining>0;}}
        public int MonsterNode {get;private set;}
        public float MoveRemaining {get;private set;}
        public float AttackRemaining {get;private set;}
        
        public float FlashBattery {get;private set;}=100;
        public float MonitorBattery {get;private set;}=100;
        public bool MonitorOpen {get;private set;}
        public int SelectedCamera {get;private set;}
        public bool Hiding {get;private set;}
        public float HideElapsed {get;private set;}
        public float HideSearch {get;private set;}
        public float HideRequired {get;private set;}=6;
        public int HideRoomUsed {get;private set;}=-1;
        public float HideCooldown {get;private set;}
        public float Awareness {get;private set;}
        
        public int Opportunities {get;private set;}
        public int LastRoll {get;private set;}
        public string Notice {get;private set;}="";
        public float NoticeRemaining {get;private set;}
        public string LossReason {get;private set;}="";
        public bool AwarenessLatched {get;private set;}
        public int Escapes {get;private set;}
        
        public float WanderRemaining {get;private set;}
        public int ApproachStage {get;private set;}
        public float RouteHold {get;private set;}
        public float LureCooldown {get;private set;}
        public float LureHold {get;private set;}
        public int LureTarget {get;private set;}=-1;
        public float WatchCharge {get;private set;}
        public float WatchDelay {get;private set;}
        public bool WatchUsed {get;private set;}
        public bool SameRoom {get{return MonsterNode==PlayerNode;}}
        public int PlayerNode {get{return PlayerRoom==0?5:PlayerRoom==1?3:4;}}
        public bool FlashOn {get{return flashHeld&&CanAct&&!MonitorOpen&&!Hiding&&!Travelling&&FlashBattery>0;}}
        public float HideSafeRemaining {get{return Hiding?Math.Max(0,tuning.hideCover-HideElapsed):0;}}
        public bool CanAct {get{return Phase==NightPhase.Playing;}}
        public string CameraHint {get{return MonsterNode<3&&SelectedCamera==MonsterNode?new[]{"SIDE PATH NEXT","PORCH NEXT","KITCHEN NEXT"}[MonsterNode]:"NO MOVEMENT IN THIS FEED";}}
        public event Action<string> Cue;
        readonly NightTuning tuning; readonly Func<double> random;
        bool flashHeld,wasNear;
        public NightGame(NightTuning settings=null,Func<double> nextRandom=null){tuning=settings??new NightTuning();var rng=new Random();random=nextRandom??new Func<double>(rng.NextDouble);}
        public void Start(int night)
        {
            Night=Math.Max(1,Math.Min(3,night));Duration=Math.Max(10,Night==1?tuning.nightOneDuration:Night==2?tuning.nightTwoDuration:tuning.nightThreeDuration);
            Phase=NightPhase.Playing;Elapsed=0;FlashBattery=MonitorBattery=100;PlayerRoom=SelectedCamera=0;MonsterNode=0;
            MonitorOpen=Hiding=flashHeld=WatchUsed=wasNear=false;
            HideElapsed=HideSearch=HideCooldown=MoveCooldown=TravelRemaining=Awareness=AttackRemaining=WanderRemaining=WatchCharge=WatchDelay=0;
            LureTarget=-1;LureCooldown=LureHold=0;RouteHold=0;AwarenessLatched=false;Escapes=0;Destination=HideRoomUsed=-1;HideRequired=6;ApproachStage=0;Opportunities=LastRoll=0;MoveRemaining=Interval();LossReason="";
            Tell("Check his entrance. The light reveals him, but also reveals you.",5);Fire("start");
        }
        public void Restart(){Start(Night);}
        float Countdown(float v,float dt){v=Math.Max(0,v-dt);return v<.000001f?0:v;}
        public void Tick(float dt){if(!CanAct||float.IsNaN(dt)||float.IsInfinity(dt)||dt<=0)return;double left=Math.Min(1,dt);while(left>.0000001&&CanAct){float slice=(float)Math.Min(.05,left);TickSlice(slice);left-=slice;}}
        void TickSlice(float dt)
        {
            Elapsed=Math.Min(Duration,Elapsed+dt);NoticeRemaining=Math.Max(0,NoticeRemaining-dt);
            if(Elapsed>=Duration){Phase=NightPhase.Won;flashHeld=false;Fire("win");return;}
            HideCooldown=Countdown(HideCooldown,dt);MoveCooldown=Countdown(MoveCooldown,dt);WatchDelay=Countdown(WatchDelay,dt);RouteHold=Countdown(RouteHold,dt);LureCooldown=Countdown(LureCooldown,dt);LureHold=Countdown(LureHold,dt);
            if(Travelling){TravelRemaining=Countdown(TravelRemaining,dt);if(!Travelling){PlayerRoom=Destination;Destination=-1;MoveCooldown=tuning.moveCooldown;Awareness=Math.Max(0,Awareness-tuning.roomChangeAwarenessDrop);if(HideRoomUsed!=PlayerRoom)HideRoomUsed=-1;wasNear=false;Fire("arrive");}}
            if(MonitorOpen){MonitorBattery=Math.Max(0,MonitorBattery-dt*tuning.cameraDrain);if(MonitorBattery<=0){MonitorOpen=false;Tell("Camera backup empty. Listen for entrances.",4);Fire("cameraDead");}}
            if(MonitorOpen&&MonsterNode<3&&SelectedCamera==MonsterNode&&!WatchUsed){WatchCharge+=dt;if(WatchCharge>=tuning.cameraWatchExposure){WatchUsed=true;WatchDelay=tuning.cameraHesitation;Tell("He hesitates under the camera. Three seconds to prepare.",3);Fire("watched");}}else WatchCharge=0;
            if(FlashOn){FlashBattery=Math.Max(0,FlashBattery-dt*tuning.flashlightDrain);if(FlashBattery<=0){flashHeld=false;Tell("Flashlight empty. Move and use cover.",4);Fire("batteryDead");}}
            if(FlashOn){Awareness=Math.Min(100,Awareness+dt*tuning.awarenessGain);if(MonsterNode>=3)WanderRemaining=0;}
            if(!FlashOn)Awareness=Math.Max(0,Awareness-dt*(Hiding?tuning.hidingAwarenessDrop:tuning.awarenessDecay));
            if(Awareness<60)AwarenessLatched=false;
            if(Awareness>=100&&!AwarenessLatched){AwarenessLatched=true;AdvanceMonster();Tell("He saw the light. Change rooms or hide.",3);Fire("attracted");}
            if(LureHold>0&&RouteHold<=0&&MonsterNode<3&&MonsterNode!=LureTarget){MonsterNode+=Math.Sign(LureTarget-MonsterNode);RouteHold=2.5f;MoveRemaining=Interval();}
            if(WatchDelay<=0){MoveRemaining-=dt;if(MoveRemaining<=0){MoveRemaining+=Interval();Opportunities++;LastRoll=1+(int)(NextRandom()*4);if(LastRoll<=Night)AdvanceMonster();}}
            if(Hiding){HideElapsed+=dt;if(MonsterNode>=3)HideSearch+=dt;
                if(HideSearch>=HideRequired){MonsterNode=PickEntrance();RouteHold=6;WanderRemaining=0;WatchUsed=false;WatchCharge=WatchDelay=0;MoveRemaining=Interval()+6;Awareness=0;AwarenessLatched=false;wasNear=false;AttackRemaining=0;Escapes++;ExitCover();HideRoomUsed=-1;Tell("He left the house. Find him again on the outdoor feeds.",4);Fire("escaped");return;}
                if(HideElapsed>=tuning.hideCover){ExitCover();Tell("You had to breathe. Find another hiding place.",3);}
            }
            if(!SameRoom){wasNear=false;AttackRemaining=0;return;}
            if(!wasNear){wasNear=true;AttackRemaining=Night==1?tuning.nightOneAttackGrace:Night==2?tuning.nightTwoAttackGrace:tuning.nightThreeAttackGrace;if(FlashBattery<=0)AttackRemaining*=tuning.darkGraceMultiplier;Fire("near");return;}
            if(!Hiding&&WanderRemaining<=0)AttackRemaining-=dt;
            if(AttackRemaining<=0){Phase=NightPhase.Lost;flashHeld=false;LossReason=MonitorOpen?"You watched the yard while he entered your room.":"The hooves stopped beside you. Escape before he finds you.";Fire("loss");}
        }
        void AdvanceMonster()
        {
            if(WatchDelay>0||RouteHold>0||LureHold>0||SameRoom)return;int before=MonsterNode;
            if(MonsterNode==0)MonsterNode=1;
            else if(MonsterNode==1)MonsterNode=2;
            else if(MonsterNode==2)MonsterNode=4;
            else if(MonsterNode==4){MonsterNode=3;}
            else if(MonsterNode==3)MonsterNode=NextRandom()<.5?4:5;
            else if(MonsterNode==5){MonsterNode=3;}
            if(before!=MonsterNode){wasNear=false;RouteHold=2.5f;MoveRemaining=Interval();if(MonsterNode>=3)Fire("hoof");}
        }
        float Interval(){return Math.Max(.2f,Night==1?tuning.nightOneMoveInterval:Night==2?tuning.nightTwoMoveInterval:tuning.nightThreeMoveInterval);}
        double NextRandom(){double n=random();return double.IsNaN(n)||double.IsInfinity(n)?.5:Math.Max(0,Math.Min(.999999,n));}
        int PickEntrance(){return (int)(NextRandom()*3);}
        public bool Lure()
        {
            if(!CanAct||!MonitorOpen||MonsterNode>=3||LureCooldown>0||MonitorBattery<tuning.lureCost)return false;
            MonitorBattery-=tuning.lureCost;LureCooldown=tuning.lureCooldown;Fire("lure");
            if(NextRandom()>=tuning.lureSuccessChance){Tell("The speaker plays. He ignores it.",3);return true;}
            LureTarget=SelectedCamera;LureHold=tuning.lureHold;RouteHold=2.5f;MoveRemaining=Interval();
            if(SelectedCamera!=MonsterNode)MonsterNode+=Math.Sign(SelectedCamera-MonsterNode);
            Tell("Speaker active. He follows the sound outside.",3);return true;
        }
        public void SetFlashlight(bool held){flashHeld=held&&CanAct&&!MonitorOpen&&!Hiding&&!Travelling&&FlashBattery>0;}
        public void ToggleMonitor(){if(!CanAct||Travelling||Hiding)return;if(!MonitorOpen&&MonitorBattery<=0){Tell("Camera backup empty.",3);return;}MonitorOpen=!MonitorOpen;flashHeld=false;Fire("camera");}
        public void SelectCamera(int camera){if(!CanAct||!MonitorOpen)return;SelectedCamera=Math.Max(0,Math.Min(2,camera));Fire("camera");}
        public void MoveTo(int room){if(!CanAct||MonitorOpen||Travelling||MoveCooldown>0||room<0||room>2||Math.Abs(room-PlayerRoom)!=1)return;if(Hiding)ExitCover();flashHeld=false;Destination=room;TravelRemaining=tuning.moveDuration;Fire("move");}
        void ExitCover(){if(!Hiding)return;Hiding=false;HideElapsed=HideSearch=0;HideCooldown=tuning.hideCooldown;Fire("unhide");}
        public void ToggleHide(){if(!CanAct||MonitorOpen||Travelling)return;if(Hiding){ExitCover();return;}if(HideCooldown>0){Tell("Catch your breath before hiding again.",2);return;}if(HideRoomUsed==PlayerRoom){Tell("He knows this hiding place. Change rooms.",3);return;}Hiding=true;HideRoomUsed=PlayerRoom;HideElapsed=HideSearch=0;HideRequired=tuning.searchBase+tuning.searchNightIncrease*(Night-1)+(int)(NextRandom()*3);flashHeld=false;Fire("hide");}
        public void TogglePause(){if(Phase==NightPhase.Playing){Phase=NightPhase.Paused;flashHeld=false;Fire("pause");}else if(Phase==NightPhase.Paused){Phase=NightPhase.Playing;Fire("resume");}}
        public void ReturnToMenu(){Phase=NightPhase.Menu;flashHeld=MonitorOpen=Hiding=false;Fire("menu");}
        void Tell(string message,float seconds){Notice=message;NoticeRemaining=seconds;}
        void Fire(string cue){if(Cue!=null)Cue(cue);}
    }
}
