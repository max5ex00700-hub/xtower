using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class XTapBattleController : MonoBehaviour
{
    enum Page { Battle, Bag, Smith, Prison }
    enum BattleState { Ready, Busy, Victory, Defeat }

    const int MaxFloor=10;
    const int PlayerMaxHp=100;
    const int PlayerAtk=5;
    const int PlayerDef=1;

    int floor=1, playerHp=100, enemyHp=100, enemyMaxHp=100;
    int enemyAtk=3, enemyDef=1;
    int prisonCount=0;
    int selectedAttack=0;
    int frameIndex=0;
    BattleState state=BattleState.Ready;
    Page page=Page.Battle;

    readonly List<string> inventory=new List<string>();
    readonly List<int> capturedFloors=new List<int>();
    readonly string[] attackPrefixes={"p","k","b"};
    readonly string[] attackNames={"주먹","차기","몸통"};
    readonly int[] rouletteValues={-50,-40,-30,-20,-10,0,10,20,30,40,50};

    Canvas canvas;
    RectTransform root;
    Image battleImage, enemyHpFill, playerHpFill, flash;
    Text title, enemyText, playerText, status, subText;
    Button[] attackButtons=new Button[3];
    Button evadeButton, captureButton, floorDownButton, floorUpButton;
    AudioSource audioSource;
    XTapOriginalApkAssets assets;

    IEnumerator Start()
    {
        Application.targetFrameRate=60;
        Screen.orientation=ScreenOrientation.Portrait;

        var assetGo=new GameObject("OriginalApkAssets");
        assets=assetGo.AddComponent<XTapOriginalApkAssets>();
        yield return assets.Load();

        audioSource=gameObject.AddComponent<AudioSource>();
        BuildUi();

        if (!assets.Ready)
        {
            ShowMissingSource();
            yield break;
        }

        Restore();
        SetupFloor(floor);
        ShowBattle();
    }

    void ShowMissingSource()
    {
        ClearRoot();
        MakePanel(root,new Color(0.025f,0.025f,0.035f,1));
        var t=MakeText(root,"원본 X탑 데이터가 없습니다.\n\nGitHub의\nunity/XTapUnity/Assets/StreamingAssets/xtop_source.apk\n위치에 X탑_v1024.apk를 올려야 합니다.",34,TextAnchor.MiddleCenter,true);
        Anchor(t.rectTransform,.08f,.25f,.92f,.75f);
    }

    void BuildUi()
    {
        if (FindObjectOfType<EventSystem>()==null)
        {
            var es=new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            DontDestroyOnLoad(es);
        }

        var go=new GameObject("Canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        go.transform.SetParent(transform,false);
        canvas=go.GetComponent<Canvas>();
        canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        var sc=go.GetComponent<CanvasScaler>();
        sc.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution=new Vector2(1080,1920);
        sc.matchWidthOrHeight=.5f;

        root=new GameObject("Root",typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(canvas.transform,false);
        Anchor(root,0,0,1,1);
    }

    void ShowBattle()
    {
        page=Page.Battle;
        ClearRoot();
        MakePanel(root,new Color(.015f,.017f,.025f,1));

        title=MakeText(root,"X탑",46,TextAnchor.MiddleLeft,true);
        Anchor(title.rectTransform,.045f,.935f,.45f,.99f);
        subText=MakeText(root,"",25,TextAnchor.MiddleRight,true);
        Anchor(subText.rectTransform,.50f,.94f,.955f,.985f);

        enemyText=MakeText(root,"",27,TextAnchor.MiddleLeft,true);
        Anchor(enemyText.rectTransform,.045f,.885f,.955f,.925f);
        enemyHpFill=MakeBar(root,.045f,.858f,.955f,.884f,new Color(.95f,.08f,.22f,1));

        battleImage=MakeImage(root,Color.black);
        battleImage.preserveAspect=true;
        Anchor(battleImage.rectTransform,.035f,.245f,.965f,.845f);

        flash=MakeImage(root,new Color(1,1,1,0));
        flash.raycastTarget=false;
        Anchor(flash.rectTransform,.035f,.245f,.965f,.845f);

        status=MakeText(root,"",34,TextAnchor.MiddleCenter,true);
        Anchor(status.rectTransform,.05f,.205f,.95f,.245f);

        playerText=MakeText(root,"",26,TextAnchor.MiddleLeft,true);
        Anchor(playerText.rectTransform,.045f,.165f,.955f,.202f);
        playerHpFill=MakeBar(root,.045f,.139f,.955f,.165f,new Color(.10f,.80f,.50f,1));

        for(int i=0;i<3;i++)
        {
            int idx=i;
            attackButtons[i]=MakeButton(root,attackNames[i],new Color(.46f,.04f,.12f,1));
            Anchor(attackButtons[i].GetComponent<RectTransform>(),.04f+i*.205f,.07f,.23f+i*.205f,.125f);
            attackButtons[i].onClick.AddListener(()=>Attack(idx));
        }

        evadeButton=MakeButton(root,"회피",new Color(.06f,.25f,.55f,1));
        Anchor(evadeButton.GetComponent<RectTransform>(),.665f,.07f,.79f,.125f);
        evadeButton.onClick.AddListener(Evade);

        captureButton=MakeButton(root,"포획",new Color(.35f,.10f,.58f,1));
        Anchor(captureButton.GetComponent<RectTransform>(),.80f,.07f,.955f,.125f);
        captureButton.onClick.AddListener(TryCapture);

        floorDownButton=MakeButton(root,"◀",new Color(.12f,.12f,.16f,1));
        Anchor(floorDownButton.GetComponent<RectTransform>(),.04f,.012f,.15f,.055f);
        floorDownButton.onClick.AddListener(()=>ChangeFloor(-1));

        var bag=MakeButton(root,"가방",new Color(.12f,.12f,.16f,1));
        Anchor(bag.GetComponent<RectTransform>(),.17f,.012f,.34f,.055f);
        bag.onClick.AddListener(ShowBag);

        var smith=MakeButton(root,"대장간",new Color(.12f,.12f,.16f,1));
        Anchor(smith.GetComponent<RectTransform>(),.36f,.012f,.57f,.055f);
        smith.onClick.AddListener(ShowSmith);

        var prison=MakeButton(root,"감옥",new Color(.12f,.12f,.16f,1));
        Anchor(prison.GetComponent<RectTransform>(),.59f,.012f,.77f,.055f);
        prison.onClick.AddListener(ShowPrison);

        floorUpButton=MakeButton(root,"▶",new Color(.12f,.12f,.16f,1));
        Anchor(floorUpButton.GetComponent<RectTransform>(),.79f,.012f,.955f,.055f);
        floorUpButton.onClick.AddListener(()=>ChangeFloor(1));

        RefreshBattle();
    }

    void RefreshBattle()
    {
        if (page!=Page.Battle) return;
        title.text="X탑  "+floor+"층";
        subText.text="감옥 "+prisonCount;
        enemyText.text=floor+"층 보스   HP "+enemyHp+" / "+enemyMaxHp+"   ATK "+enemyAtk+"   DEF "+enemyDef;
        playerText.text="조훈   HP "+playerHp+" / "+PlayerMaxHp+"   ATK "+PlayerAtk+"   DEF "+PlayerDef;
        enemyHpFill.fillAmount=enemyHp/(float)Math.Max(1,enemyMaxHp);
        playerHpFill.fillAmount=playerHp/(float)PlayerMaxHp;
        captureButton.gameObject.SetActive(state==BattleState.Victory);
        floorDownButton.interactable=floor>1;
        floorUpButton.interactable=floor<MaxFloor && state==BattleState.Victory;
        SetBattleSprite(state==BattleState.Victory ? "cap" : attackPrefixes[selectedAttack], frameIndex);
    }

    void Attack(int kind)
    {
        if (state!=BattleState.Ready) return;
        selectedAttack=kind;
        StartCoroutine(AttackRoutine(kind));
    }

    IEnumerator AttackRoutine(int kind)
    {
        state=BattleState.Busy;
        SetInteractable(false);
        status.text=attackNames[kind]+"!";
        for(int i=0;i<3;i++)
        {
            frameIndex=UnityEngine.Random.Range(0,10);
            SetBattleSprite(attackPrefixes[kind],frameIndex);
            yield return new WaitForSeconds(.085f);
        }

        bool dodged=UnityEngine.Random.value<.20f;
        if (dodged)
        {
            status.text="보스 회피!";
            frameIndex=UnityEngine.Random.Range(0,10);
            SetBattleSprite("d",frameIndex);
            Play("res/raw/dodge.wav");
            yield return new WaitForSeconds(.30f);
        }
        else
        {
            int damage=Math.Max(1,PlayerAtk-enemyDef)+UnityEngine.Random.Range(3,8);
            enemyHp=Math.Max(0,enemyHp-damage);
            status.text=damage+" 데미지";
            Play("assets/hit.wav");
            yield return HitFx();

            if (enemyHp<=0)
            {
                state=BattleState.Victory;
                status.text="승리! 보상을 확인하십시오.";
                SetBattleSprite("cap",0);
                Play("assets/win.wav");
                GiveRouletteReward();
                Save();
                RefreshBattle();
                yield break;
            }
        }

        int taken=Math.Max(1,enemyAtk-PlayerDef);
        playerHp=Math.Max(0,playerHp-taken);
        if(playerHp<=0)
        {
            state=BattleState.Defeat;
            status.text="패배";
            Play("assets/hurt.wav");
            yield return RedFx();
            yield return new WaitForSeconds(.5f);
            playerHp=PlayerMaxHp;
            SetupFloor(floor);
        }
        else state=BattleState.Ready;

        SetInteractable(true);
        Save();
        RefreshBattle();
    }

    void Evade()
    {
        if(state!=BattleState.Ready)return;
        StartCoroutine(EvadeRoutine());
    }

    IEnumerator EvadeRoutine()
    {
        state=BattleState.Busy;
        SetInteractable(false);
        status.text="회피!";
        for(int i=0;i<4;i++)
        {
            frameIndex=UnityEngine.Random.Range(0,10);
            SetBattleSprite("d",frameIndex);
            yield return new WaitForSeconds(.08f);
        }
        Play("res/raw/dodge.wav");
        status.text="회피 성공";
        yield return new WaitForSeconds(.25f);
        state=BattleState.Ready;
        SetInteractable(true);
        RefreshBattle();
    }

    void TryCapture()
    {
        if(state!=BattleState.Victory)return;
        if(!capturedFloors.Contains(floor))
        {
            capturedFloors.Add(floor);
            prisonCount=capturedFloors.Count;
            status.text=floor+"층 보스 포획 완료";
            inventory.Add(floor+"층 포획 증표");
            Play("assets/sparkle.wav");
            Save();
            RefreshBattle();
        }
        else status.text="이미 감옥에 있습니다.";
    }

    void GiveRouletteReward()
    {
        int result=rouletteValues[UnityEngine.Random.Range(0,rouletteValues.Length)];
        if(result==0)
        {
            status.text="룰렛: 포획 기회!";
            return;
        }
        string gear=(result>0?"+":"")+result+"% 장비";
        inventory.Add(gear);
        status.text="룰렛 "+gear+" 획득";
    }

    void ShowBag()
    {
        page=Page.Bag;
        BuildSimplePage("가방","보유 장비 "+inventory.Count+"개",()=>{
            float top=.78f;
            int max=Math.Min(12,inventory.Count);
            for(int i=0;i<max;i++)
            {
                var p=MakePanel(root,new Color(.08f,.085f,.11f,1));
                float row=i/3, col=i%3;
                Anchor(p.rectTransform,.055f+col*.305f,top-row*.12f,.335f+col*.305f,top+.09f-row*.12f);
                var t=MakeText(p.transform,inventory[i],22,TextAnchor.MiddleCenter,true);
                Anchor(t.rectTransform,.05f,.05f,.95f,.95f);
            }
            if(inventory.Count==0)
            {
                var t=MakeText(root,"아직 장비가 없습니다.\n전투 승리 후 룰렛에서 획득합니다.",30,TextAnchor.MiddleCenter,false);
                Anchor(t.rectTransform,.1f,.35f,.9f,.65f);
            }
        });
    }

    void ShowSmith()
    {
        page=Page.Smith;
        BuildSimplePage("대장간","슬롯 강화",()=>{
            var card=MakePanel(root,new Color(.07f,.075f,.10f,1));
            Anchor(card.rectTransform,.08f,.38f,.92f,.75f);
            var t=MakeText(card.transform,"장비를 투입하면\n슬롯 결과에 따라 보정치가 변합니다.",31,TextAnchor.MiddleCenter,true);
            Anchor(t.rectTransform,.08f,.50f,.92f,.92f);
            var b=MakeButton(card.transform,"슬롯 돌리기",new Color(.45f,.16f,.05f,1));
            Anchor(b.GetComponent<RectTransform>(),.18f,.10f,.82f,.35f);
            b.onClick.AddListener(()=>{
                int v=rouletteValues[UnityEngine.Random.Range(0,rouletteValues.Length)];
                if(v==0)v=10;
                t.text="대장간 결과\n"+(v>0?"+":"")+v+"%";
                Play("res/raw/tick.wav");
            });
        });
    }

    void ShowPrison()
    {
        page=Page.Prison;
        BuildSimplePage("감옥","포획 "+capturedFloors.Count+"명",()=>{
            if(capturedFloors.Count==0)
            {
                var t=MakeText(root,"포획된 보스가 없습니다.",31,TextAnchor.MiddleCenter,false);
                Anchor(t.rectTransform,.1f,.40f,.9f,.65f);
                return;
            }

            int f=capturedFloors[capturedFloors.Count-1];
            var img=MakeImage(root,Color.black);
            img.preserveAspect=true;
            Anchor(img.rectTransform,.10f,.25f,.90f,.78f);
            var sp=assets.GetSprite("assets/f"+f+"_cap.jpg");
            if(sp!=null)img.sprite=sp;
            var t2=MakeText(root,f+"층 보스",32,TextAnchor.MiddleCenter,true);
            Anchor(t2.rectTransform,.1f,.17f,.9f,.23f);
        });
    }

    void BuildSimplePage(string heading,string small,Action body)
    {
        ClearRoot();
        MakePanel(root,new Color(.015f,.017f,.025f,1));
        var h=MakeText(root,heading,48,TextAnchor.MiddleLeft,true);
        Anchor(h.rectTransform,.055f,.91f,.60f,.98f);
        var s=MakeText(root,small,26,TextAnchor.MiddleRight,false);
        Anchor(s.rectTransform,.55f,.915f,.945f,.975f);
        var back=MakeButton(root,"전투로",new Color(.18f,.18f,.23f,1));
        Anchor(back.GetComponent<RectTransform>(),.055f,.035f,.30f,.095f);
        back.onClick.AddListener(ShowBattle);
        body();
    }

    void ChangeFloor(int d)
    {
        int nf=floor+d;
        if(nf<1||nf>MaxFloor)return;
        if(d>0 && state!=BattleState.Victory)return;
        floor=nf;
        playerHp=PlayerMaxHp;
        SetupFloor(floor);
        Save();
        RefreshBattle();
    }

    void SetupFloor(int f)
    {
        float mul=Mathf.Pow(1.35f,f-1);
        enemyMaxHp=Mathf.RoundToInt(100*mul);
        enemyHp=enemyMaxHp;
        enemyAtk=Mathf.Max(3,Mathf.RoundToInt(3*mul));
        enemyDef=Mathf.Max(1,Mathf.RoundToInt(1*mul));
        state=BattleState.Ready;
        frameIndex=0;
        selectedAttack=0;
        statusTextSafe("전투 준비");
    }

    void SetBattleSprite(string prefix,int index)
    {
        if(battleImage==null || assets==null || !assets.Ready)return;
        string entry;
        if(prefix=="cap") entry="assets/f"+floor+"_cap.jpg";
        else entry="assets/f"+floor+"_"+prefix+index.ToString("00")+".jpg";
        var sp=assets.GetSprite(entry);
        if(sp!=null){battleImage.sprite=sp;battleImage.color=Color.white;}
    }

    IEnumerator HitFx()
    {
        flash.color=new Color(1,.85f,.55f,.65f);
        yield return new WaitForSeconds(.06f);
        flash.color=new Color(1,1,1,0);
    }

    IEnumerator RedFx()
    {
        flash.color=new Color(1,.02f,.05f,.55f);
        yield return new WaitForSeconds(.10f);
        flash.color=new Color(1,1,1,0);
    }

    void SetInteractable(bool v)
    {
        foreach(var b in attackButtons) if(b!=null)b.interactable=v;
        if(evadeButton!=null)evadeButton.interactable=v;
    }

    void Play(string entry)
    {
        var clip=assets.GetWav(entry);
        if(clip!=null)audioSource.PlayOneShot(clip);
    }

    void Save()
    {
        PlayerPrefs.SetInt("floor",floor);
        PlayerPrefs.SetInt("prison",prisonCount);
        PlayerPrefs.SetString("captured",string.Join(",",capturedFloors));
        PlayerPrefs.SetString("inventory",string.Join("|",inventory));
        PlayerPrefs.Save();
    }

    void Restore()
    {
        floor=Mathf.Clamp(PlayerPrefs.GetInt("floor",1),1,MaxFloor);
        var cap=PlayerPrefs.GetString("captured","");
        if(!string.IsNullOrEmpty(cap))
            foreach(var s in cap.Split(',')){int v;if(int.TryParse(s,out v)&&!capturedFloors.Contains(v))capturedFloors.Add(v);}
        prisonCount=capturedFloors.Count;
        var inv=PlayerPrefs.GetString("inventory","");
        if(!string.IsNullOrEmpty(inv))inventory.AddRange(inv.Split('|'));
    }

    void statusTextSafe(string s){if(status!=null)status.text=s;}

    void ClearRoot()
    {
        for(int i=root.childCount-1;i>=0;i--)Destroy(root.GetChild(i).gameObject);
    }

    Image MakeImage(Transform parent,Color c)
    {
        var go=new GameObject("Image",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
        go.transform.SetParent(parent,false);var i=go.GetComponent<Image>();i.color=c;return i;
    }

    Image MakeBar(Transform parent,float x1,float y1,float x2,float y2,Color c)
    {
        var back=MakeImage(parent,new Color(.12f,.12f,.15f,1));Anchor(back.rectTransform,x1,y1,x2,y2);
        var fill=MakeImage(back.transform,c);Anchor(fill.rectTransform,0,0,1,1);fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Horizontal;return fill;
    }

    Image MakePanel(Transform parent,Color c){return MakeImage(parent,c);}

    Text MakeText(Transform parent,string s,int size,TextAnchor a,bool bold)
    {
        var go=new GameObject("Text",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));go.transform.SetParent(parent,false);
        var t=go.GetComponent<Text>();t.text=s;t.font=Resources.GetBuiltinResource<Font>("Arial.ttf");t.fontSize=size;t.alignment=a;t.color=Color.white;t.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;
    }

    Button MakeButton(Transform parent,string label,Color c)
    {
        var go=new GameObject("Button",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);
        go.GetComponent<Image>().color=c;var b=go.GetComponent<Button>();
        var t=MakeText(go.transform,label,27,TextAnchor.MiddleCenter,true);Anchor(t.rectTransform,0,0,1,1);return b;
    }

    static void Anchor(RectTransform r,float x1,float y1,float x2,float y2)
    {
        r.anchorMin=new Vector2(x1,y1);r.anchorMax=new Vector2(x2,y2);r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;
    }
}
