// Adapters deliberately model no Unity rendering/audio. Methods under test are
// extracted verbatim from XTapBattleController.cs by run.py.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class CombatProbe
{
    public readonly XTapCombatState combatState = new XTapCombatState();
    readonly XTapCombatPointer combatPointer = new XTapCombatPointer();
    public readonly Fx combatFx = new Fx();
    AudioSource combatSfxSource = new AudioSource();
    AudioClip shieldClang = new AudioClip();
    public readonly Gacha gachaMachine = new Gacha();
    public bool busy, won, appPaused, appUnfocused, hitStopActive;
    bool sfxEnabled = true;
    const float CharacterDodgeChance = .2f;
    Vector2 pointerStart, weakNorm = new Vector2(.5f,.5f), shieldNorm = new Vector2(.5f,.5f);
    float pointerStartTime;
    bool weakActive { get { return combatState.IsHitCue; } }
    bool shieldActive { get { return combatState.Cue == XTapCombatCue.Shield; } }
    public double enemyHp = 1000d, enemyMaxHp = 1000d, enemyDefense = 2d;
    public int hitCount, fightCount, currentStep, maxUnlockedStep, saves, counters;
    public bool shieldNext;
    readonly string[] criticalHitVoices = {}, lowHpVoices = {}, swipeHitVoices = {}, normalHitVoices = {};
    readonly List<Stack<IEnumerator>> routines = new List<Stack<IEnumerator>>();
    public float waits;
    public void Down(int id, float x = 540f, float y = 960f) { CombatPointerDown(id, new Vector2(x,y)); }
    public void Up(int id, bool canceled = false, float x = 540f, float y = 960f) { CombatPointerUp(id,new Vector2(x,y),canceled); }
    void StartCoroutine(IEnumerator routine)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(routine); routines.Add(stack); Step(stack);
    }
    void Step(Stack<IEnumerator> stack)
    {
        while (stack.Count > 0)
        {
            var top = stack.Peek();
            if (!top.MoveNext()) { stack.Pop(); continue; }
            var nested = top.Current as IEnumerator;
            if (nested != null) { stack.Push(nested); continue; }
            var wait = top.Current as WaitForSecondsRealtime;
            if (wait != null) { waits += wait.seconds; Time.unscaledTime += wait.seconds; }
            return;
        }
    }
    public void Drain()
    {
        for (int limit=0; limit<1000 && routines.Count>0; limit++)
        {
            combatState.Advance(.016d, busy || hitStopActive);
            foreach (var stack in routines.ToArray()) Step(stack);
            routines.RemoveAll(s=>s.Count==0);
        }
        if (routines.Count>0) throw new Exception("stuck coroutine");
    }
    void HideWeakPoint() { if (weakActive) combatState.CloseCue(); }
    void HideShieldPoint() { if (shieldActive) combatState.CloseCue(); }
    void ShowHitCue(XTapCombatCue cue) { combatState.OpenCue(cue); }
    void StartWeakPoint() { ShowHitCue(XTapCombatCue.Hit); }
    void StartFollowupWeakPoint() { ShowHitCue(XTapCombatCue.Followup); }
    bool TryStartShieldWindow() { if (!shieldNext || weakActive || shieldActive || won) return false; shieldNext=false; combatState.OpenCue(XTapCombatCue.Shield); return true; }
    bool ApplyEnemyCounterAttack() { counters++; return false; }
    IEnumerator CharacterRecoil(Vector2 at,bool heavy,bool dodge,int tier=0) { yield return new WaitForSecondsRealtime(.13f); }
    void ResetJelly() {}
    int ZoneOf(Vector2 at) { return at.x < 100f || at.x > 980f ? 6 : 1; }
    void SetActionSprite(string s) {}
    void SetStageOrFallback(int s) {}
    int Stage() { return 0; }
    void ShowBubble(string text,float seconds,Vector2? position=null) {}
    void PlayCombatSfx(string id,float volume) {}
    void PlayVoice(string id,float volume) {}
    void PlayCombatImpact(string prefix,bool swipe,bool heavy) {}
    void PlayRandomVoice(string[] ids,float volume) {}
    void Play(string name) {}
    void VibrateTouch(bool heavy) {}
    void StartJellyImpact(Vector2 at,bool heavy,bool swipe,Vector2 delta) {}
    void RefreshBattleStatUi() {}
    void RefreshMainProgressUi() {}
    void SaveProgress() { saves++; }
    int CurrentCharacterId() { return 1; }
    double CurrentPlayerAttack() { return 10d; }
    double SafeMultiply(double a,double b) { return a*b; }
    string PrefixFor(int zone,bool swipe,Vector2 delta) { return "p"; }
    string MixedDodgeLine() { return "dodge"; }
    string MixedCriticalLine() { return "critical"; }
    string MixedCombatLine(int zone) { return "hit"; }
    string MixedSurrenderLine(int id) { return "finish"; }
    public sealed class Fx
    {
        public bool Frozen;
        public int hits, finishes, misses, guards, perfects, counterReady, dodges;
        public double damage;
        public void Hit(Vector2 at,double amount,XTapCombatCue cue,int combo) { hits++; damage+=amount; }
        public void Finish(Vector2 at) { finishes++; }
        public void Miss(Vector2 at,string text) { misses++; }
        public void Guard(Vector2 at,bool perfect) { guards++; if(perfect)perfects++; }
        public void CounterReady(Vector2 at) { counterReady++; }
        public void Dodge(Vector2 at) { dodges++; }
        public void Clear() {}
    }
    public sealed class Gacha { public int calls; public void PlayReward(int id,int step) { calls++; } }
}

static class CombatProbeChecks
{
    static int count;
    static void Check(bool ok,string reason) { count++; if(!ok)throw new Exception(reason); }
    public static int Run()
    {
        UnityEngine.Random.value = 1f;
        var p = new CombatProbe();
        p.combatState.OpenCue(XTapCombatCue.Hit);
        p.Down(1);
        Check(p.combatFx.hits==1 && p.enemyHp==966d,"HIT damage applied on down immediately");
        Check(p.busy && p.hitStopActive && p.combatFx.Frozen,"hit-stop freezes presentation");
        p.Up(1); p.Drain();
        Check(p.combatFx.hits==1 && p.combatState.Cue==XTapCombatCue.Followup,"release cannot double-hit; followup opens");
        Check(p.combatState.Remaining==.7d,"followup starts with full lifetime");
        p.Down(2); p.Down(3); p.Up(3); p.Up(2); p.Drain();
        Check(p.combatFx.hits==2 && p.enemyHp==918d,"followup five-times attack once despite multi-touch");
        Check(p.counters==1 && p.combatState.Combo==2,"single counter after followup and combo retained");
        p=new CombatProbe();p.Down(1);p.Up(1,true);p.Drain();
        Check(p.combatFx.hits==0,"canceled gesture cannot attack");
        p.Down(2);p.Up(2,false,740f,960f);p.Drain();
        Check(p.enemyHp==986d && p.combatFx.hits==1,"ordinary swipe still resolves once");
        p=new CombatProbe();p.combatState.OpenCue(XTapCombatCue.Shield);p.combatState.Advance(.87d,false);
        p.Down(1);p.Up(1);p.Drain();
        Check(p.combatFx.perfects==1 && p.combatState.Cue==XTapCombatCue.Counter,"last .18 seconds opens counter HIT");
        Check(p.combatState.Remaining>1.049d && p.combatFx.counterReady==1,"counter timer starts after freeze");
        Check(p.counters==0 && p.combatFx.hits==0,"perfect guard applies neither attack damage nor enemy penalty");
        p.Down(2);p.Up(2);p.Drain();
        Check(p.enemyHp==952d && p.combatFx.hits==1 && p.combatState.Cue!=XTapCombatCue.Followup,"counter deals 5x without extra followup chain");
        p=new CombatProbe();p.combatState.OpenCue(XTapCombatCue.Shield);p.combatState.Advance(.5d,false);
        p.Down(1);p.Up(1);p.Drain();
        Check(p.combatFx.guards==1 && p.combatFx.perfects==0 && p.combatState.Cue==XTapCombatCue.None,"early guard is ordinary block");
        p=new CombatProbe();p.combatState.OpenCue(XTapCombatCue.Shield);p.combatState.RegisterHit();
        p.Down(1,200f,960f);p.Up(1);p.Drain();
        Check(p.combatState.Combo==0 && p.combatState.Cue==XTapCombatCue.Shield && p.combatFx.hits==0,"wrong shield touch cannot attack or consume shield");
        p=new CombatProbe();for(int i=0;i<6;i++)p.combatState.RegisterHit();
        UnityEngine.Random.value=0f;p.Down(1);p.Up(1);p.Drain();UnityEngine.Random.value=1f;
        Check(p.combatState.Combo==6 && p.combatFx.dodges==1,"random dodge does not reset combo");
        p=new CombatProbe();p.combatState.OpenCue(XTapCombatCue.Hit);p.combatState.Advance(.2d,false);
        double remaining=p.combatState.Remaining;
        p.Down(1,200f,960f);p.Up(1,false,200f,960f);p.Drain();
        Check(p.combatState.Cue==XTapCombatCue.Hit && Math.Abs(p.combatState.Remaining-remaining)<1e-9d,"ordinary-hit presentation cannot expire open HIT");
        p=new CombatProbe();p.enemyHp=8d;p.Down(1);p.Up(1);
        Check(p.won && p.combatState.Finished && p.combatFx.finishes==1,"actual lethal damage claims finish immediately");
        p.Down(2);p.Up(2);p.Drain();
        Check(p.gachaMachine.calls==1 && p.saves==1 && p.currentStep==1 && p.fightCount==1,"one floor and one reward on finish");
        Check(Math.Abs(p.waits-.6f)<.0001f,"finisher contact sequence totals .6 seconds");
        p.Down(3);p.Up(3);p.Drain();Check(p.gachaMachine.calls==1 && p.combatFx.hits==1,"post-victory touches cannot duplicate rewards");
        p=new CombatProbe();p.Down(1);p.Up(1);p.Down(2);p.Drain();p.Up(2);p.Drain();
        Check(p.combatFx.hits==1,"touch begun during busy cannot replay on release");
        return count;
    }
}
namespace UnityEngine
{
    public struct Vector2
    {
        public float x,y;
        public Vector2(float x,float y){this.x=x;this.y=y;}
        public static Vector2 zero {get{return new Vector2();}}
        public float magnitude {get{return (float)Math.Sqrt(x*x+y*y);}}
        public static Vector2 operator -(Vector2 a,Vector2 b){return new Vector2(a.x-b.x,a.y-b.y);}
        public static Vector2 Lerp(Vector2 a,Vector2 b,float t){return new Vector2(a.x+(b.x-a.x)*t,a.y+(b.y-a.y)*t);}
        public static float Distance(Vector2 a,Vector2 b){return (a-b).magnitude;}
    }
    public static class Screen { public static int width=1080,height=1920; }
    public static class Time { public static float unscaledTime; }
    public static class Random { public static float value=1f; }
    public static class Mathf
    {
        public static float Max(float a,float b){return Math.Max(a,b);}
        public static int Max(int a,int b){return Math.Max(a,b);}
    }
    public sealed class WaitForSecondsRealtime {public float seconds;public WaitForSecondsRealtime(float s){seconds=s;}}
    public sealed class AudioClip {}
    public sealed class AudioSource {public float pitch;public void PlayOneShot(AudioClip clip,float volume){}}
}
