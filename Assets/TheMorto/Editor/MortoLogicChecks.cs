using System;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif
namespace TheMorto.Editor
{
    public static class MortoLogicChecks
    {
#if UNITY_EDITOR
        [MenuItem("The Morto/Run Logic Checks")]
        public static void RunFromMenu(){int failed=RunAll(m=>Debug.Log(m));if(failed>0)Debug.LogError(failed+" Morto checks failed.");}
#endif
        static void Set(NightGame g,string property,object value){typeof(NightGame).GetProperty(property).SetValue(g,value,null);}
        static void Advance(NightGame g){Set(g,"RouteHold",0f);typeof(NightGame).GetMethod("AdvanceMonster",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(g,null);}
        static NightGame Make(int n=1,Func<double> random=null){var g=new NightGame(null,random??new Func<double>(()=>0));g.Start(n);return g;}
        static void Step(NightGame g,float t){while(t>.00001f){float d=Math.Min(.05f,t);g.Tick(d);t-=d;}}
        static void Encounter(NightGame g){Set(g,"MonsterNode",g.PlayerNode);Set(g,"MoveRemaining",999f);g.Tick(.01f);}
        static void Require(bool v,string m="State assertion failed"){if(!v)throw new Exception(m);}
        static void Near(float a,float b){Require(Math.Abs(a-b)<.02f,a+" != "+b);}
        public static int RunAll(Action<string> report)
        {
            int passed=0,failed=0;Action<string,Action> check=(name,fn)=>{try{fn();passed++;report("PASS "+name);}catch(Exception e){failed++;report("FAIL "+name+" / "+e.Message);}};
            check("Menu and invalid deltas do not simulate",()=>{var g=new NightGame();g.Tick(1);Near(g.Elapsed,0);g.Start(1);g.Tick(float.NaN);g.Tick(float.PositiveInfinity);g.Tick(-1);Near(g.Elapsed,0);});
            check("Three night durations and clamp",()=>{var g=Make();for(int n=1;n<=3;n++){g.Start(n);Near(g.Duration,n==1?150:n==2?165:180);}g.Start(99);Require(g.Night==3);});
            check("Aggression compared with all four uniform rolls",()=>{for(int n=1;n<=3;n++)for(int r=1;r<=4;r++){int roll=r;var g=Make(n,()=>(roll-.5)/4);Set(g,"MoveRemaining",.001f);g.Tick(.01f);Require(g.LastRoll==r&&g.MonsterNode==(r<=n?1:0));}});
            check("Fixed route covers all cameras kitchen hallway living with indoor-only hoof cues",()=>{var g=Make(1,()=>.75);int[] expected={1,2,4,3,5};int hoofs=0;g.Cue+=c=>{if(c=="hoof"){Require(g.MonsterNode>=3);hoofs++;}};foreach(int node in expected){Advance(g);Require(g.MonsterNode==node);}Require(hoofs==3);});
            check("Every approach starts at the garden",()=>{for(int i=0;i<3;i++){int seed=i;Require(Make(1,()=>(seed+.1)/3).MonsterNode==0);}});
            check("Camera hints require the occupied feed",()=>{var g=Make();g.ToggleMonitor();Require(g.CameraHint.Contains("SIDE PATH"));g.SelectCamera(1);Require(g.CameraHint.Contains("NO MOVEMENT"));});
            check("Watching correct camera buys one three-second hesitation",()=>{var g=Make();Set(g,"MoveRemaining",999f);g.ToggleMonitor();Step(g,1.3f);Require(g.WatchDelay>2.9f);Advance(g);Require(g.ApproachStage==0);Step(g,4);Near(g.WatchDelay,0);g.ToggleMonitor();g.ToggleMonitor();Step(g,2);Near(g.WatchDelay,0);});
            check("Wrong camera cannot cause hesitation",()=>{var g=Make();Set(g,"MoveRemaining",999f);g.ToggleMonitor();g.SelectCamera(1);Step(g,2);Require(!g.WatchUsed);});
            check("Monitor drains its own battery only",()=>{var g=Make();Set(g,"MoveRemaining",999f);g.ToggleMonitor();Step(g,1);Near(g.MonitorBattery,98.2f);Near(g.FlashBattery,100);});
            check("Empty monitor closes and cannot reopen",()=>{var g=Make();Set(g,"MonitorBattery",.01f);g.ToggleMonitor();g.Tick(.05f);Require(!g.MonitorOpen&&g.Phase==NightPhase.Playing);g.ToggleMonitor();Require(!g.MonitorOpen);});
            check("Movement is adjacent and takes one second",()=>{var g=Make();g.MoveTo(2);Require(!g.Travelling);g.MoveTo(1);Step(g,.95f);Require(g.PlayerRoom==0);Step(g,.06f);Require(g.PlayerRoom==1);Near(g.MoveCooldown,1.5f);});
            check("Travel and monitor prevent flashlight use",()=>{var g=Make();g.MoveTo(1);g.SetFlashlight(true);Require(!g.FlashOn);Step(g,2.6f);g.ToggleMonitor();g.SetFlashlight(true);Require(!g.FlashOn);});
            check("Flashlight drains only while held",()=>{var g=Make();Set(g,"MoveRemaining",999f);g.SetFlashlight(true);Step(g,1);Near(g.FlashBattery,94.5f);g.SetFlashlight(false);Step(g,1);Near(g.FlashBattery,94.5f);});
            check("Light cannot stun or expel a close creature",()=>{var g=Make();Encounter(g);float before=g.AttackRemaining;g.SetFlashlight(true);Step(g,.8f);Require(g.SameRoom);Near(g.AttackRemaining,before-.8f);});
            check("Flashlight camping ends in defeat",()=>{var g=Make();Encounter(g);g.SetFlashlight(true);Step(g,4);Require(g.Phase==NightPhase.Lost);});
            check("Awareness takes about 3 seconds to fill",()=>{var g=Make();Set(g,"MoveRemaining",999f);g.SetFlashlight(true);Step(g,1);Near(g.Awareness,33.6f);Step(g,4.7f);Near(g.Awareness,100);});
            check("Full awareness stays latched without repeated advances",()=>{var g=Make();Set(g,"MoveRemaining",999f);g.SetFlashlight(true);Step(g,3.1f);Set(g,"MoveRemaining",999f);Step(g,4);Near(g.Awareness,100);Require(g.MonsterNode==1&&g.ApproachStage==0);});
            check("Darkness naturally cools awareness",()=>{var g=Make();Set(g,"MoveRemaining",999f);g.SetFlashlight(true);Step(g,1);g.SetFlashlight(false);Step(g,1);Near(g.Awareness,21.6f);});
            check("Room change lowers awareness by half",()=>{var g=Make();Set(g,"Awareness",100f);g.MoveTo(1);Step(g,1.1f);Near(g.Awareness,36.8f);});
            check("Hiding lowers awareness rapidly",()=>{var g=Make();Set(g,"MoveRemaining",999f);Set(g,"Awareness",100f);g.ToggleHide();Step(g,2);Near(g.Awareness,30);});
            check("Hide needs 6-12 actual search seconds before wandering",()=>{for(int n=1;n<=3;n++){var g=Make(n);Encounter(g);g.ToggleHide();Near(g.HideRequired,6+2*(n-1));Step(g,g.HideRequired-.2f);Require(g.SameRoom&&g.Hiding);Step(g,.3f);Require(!g.SameRoom&&!g.Hiding&&g.MonsterNode==0&&g.Escapes==1);}});
            check("Hiding outdoors cannot remotely repel Morto",()=>{var g=Make();Set(g,"MoveRemaining",999f);g.ToggleHide();Step(g,10);Require(g.MonsterNode==0);Near(g.HideSearch,0);});
            check("Breath ends after fifteen seconds",()=>{var g=Make();Set(g,"MoveRemaining",999f);g.ToggleHide();Step(g,15.1f);Require(!g.Hiding&&g.HideCooldown>7);});
            check("Same cover requires a room change before reuse",()=>{var g=Make();Set(g,"MoveRemaining",999f);g.ToggleHide();g.ToggleHide();Step(g,8.1f);g.ToggleHide();Require(!g.Hiding);g.MoveTo(1);Step(g,1.1f);g.ToggleHide();Require(g.Hiding);});
            check("Hiding disables light and monitor",()=>{var g=Make();g.ToggleHide();g.SetFlashlight(true);g.ToggleMonitor();Require(!g.FlashOn&&!g.MonitorOpen);});
            check("Successful hide resets the route outside and clears awareness",()=>{var g=Make();Encounter(g);g.ToggleHide();Step(g,6.2f);Require(g.MonsterNode==0&&g.WanderRemaining==0&&g.HideRoomUsed==-1);Near(g.Awareness,0);Advance(g);Require(g.MonsterNode==1);});
            check("Hiding can reset Morto from another indoor room",()=>{var g=Make();Set(g,"MonsterNode",4);Set(g,"MoveRemaining",999f);g.ToggleHide();Step(g,6.2f);Require(g.MonsterNode==0&&g.Escapes==1);});
            check("Empty flashlight still allows hiding",()=>{var g=Make();Set(g,"FlashBattery",0f);Encounter(g);g.ToggleHide();Step(g,6.2f);Require(g.Phase==NightPhase.Playing&&!g.SameRoom);});
            check("Pause freezes search and power",()=>{var g=Make();Encounter(g);g.ToggleHide();g.TogglePause();float age=g.HideElapsed,elapsed=g.Elapsed;Step(g,2);Near(g.HideElapsed,age);Near(g.Elapsed,elapsed);});
            check("Restart clears new hazards",()=>{var g=Make();g.ToggleHide();Step(g,2);g.Restart();Require(g.HideRoomUsed==-1&&!g.AwarenessLatched&&g.Escapes==0);Near(g.HideSearch,0);});
            check("Hallway randomly chooses either adjacent indoor room",()=>{for(int i=0;i<2;i++){int side=i;var g=Make(1,()=>side==0?0:.9);Set(g,"MonsterNode",3);Advance(g);Require(g.MonsterNode==(i==0?4:5));}});
            check("Hide chooses any outdoor camera and grants recovery time",()=>{for(int i=0;i<3;i++){int cam=i;var g=Make(1,()=>(cam+.1)/3);Encounter(g);g.ToggleHide();Step(g,g.HideRequired+.1f);Require(g.MonsterNode==i&&g.MoveRemaining>9);}});
            check("Speaker has 70 percent success and failed attempts preserve AI",()=>{int successes=0;for(int i=0;i<100;i++){double sample=i/100.0;var g=Make(1,()=>sample);g.ToggleMonitor();Set(g,"MonsterNode",2);g.SelectCamera(0);Set(g,"MoveRemaining",3f);int cues=0;g.Cue+=cue=>{if(cue=="lure")cues++;};Require(g.Lure());Near(g.MonitorBattery,94);Near(g.LureCooldown,10);Require(cues==1);if(i<70){successes++;Require(g.MonsterNode==1);Near(g.LureHold,5);}else{Require(g.MonsterNode==2&&g.LureTarget==-1);Near(g.MoveRemaining,3);Near(g.LureHold,0);Near(g.RouteHold,0);}}Require(successes==70);});
            check("Speaker attracts along outdoor cameras and obeys cost and cooldown",()=>{var g=Make();Require(!g.Lure());g.ToggleMonitor();Set(g,"MonsterNode",2);g.SelectCamera(0);Require(g.Lure()&&g.MonsterNode==1);Near(g.MonitorBattery,94);Require(!g.Lure());Step(g,2.6f);Require(g.MonsterNode==0);Set(g,"LureCooldown",0f);Set(g,"MonitorBattery",5f);Require(!g.Lure());Set(g,"MonitorBattery",100f);Set(g,"MonsterNode",4);Require(!g.Lure());});
            for(int n=1;n<=3;n++){int night=n;check("Active room changes and hiding survive full night "+n,()=>{var g=Make(night);int moves=0;for(int i=0;i<5000&&g.Phase==NightPhase.Playing;i++){if(!g.Hiding&&!g.Travelling){if(g.HideRoomUsed==g.PlayerRoom&&g.MoveCooldown==0){g.MoveTo(g.PlayerRoom==0?1:g.PlayerRoom==2?1:2);moves++;}else if(g.MonsterNode>=3&&g.HideCooldown==0)g.ToggleHide();}g.Tick(.05f);}Require(g.Phase==NightPhase.Won&&g.Escapes>0);});}
            report("The Morto: "+passed+" passed / "+failed+" failed.");return failed;
        }
    }
}
