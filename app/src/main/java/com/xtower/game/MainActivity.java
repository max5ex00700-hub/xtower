package com.xtower.game;

import android.app.*;
import android.os.*;
import android.graphics.*;
import android.view.*;
import android.media.*;
import android.content.res.AssetFileDescriptor;
import java.util.*;

public class MainActivity extends Activity {
    Game g;
    @Override public void onCreate(Bundle b){
        super.onCreate(b);
        getWindow().getDecorView().setSystemUiVisibility(5894);
        g=new Game(); setContentView(g);
    }
    @Override protected void onDestroy(){ super.onDestroy(); if(g!=null) g.releaseAudio(); }

    class Game extends View {
        Paint p=new Paint(Paint.ANTI_ALIAS_FLAG); Random r=new Random();
        Bitmap[] stage=new Bitmap[5];
        SoundPool pool; HashMap<String,Integer> snd=new HashMap<>();
        int floor=1,maxFloor=1,hp=100,maxHp=100,atk=5,def=3;
        int ehp=40,emax=40,eatk=8,edef=2;
        boolean inCombat=false,cleared=false,pendingChest=false;
        long fxUntil=0,bubbleUntil=0,flashUntil=0;
        String fxKind="",atkName="",bubble="",log="X탑에 진입했습니다.";
        int lastDeal=0;
        String[] names={"아이린","이리스","마를렌","노에","카렌","비비안","루나","에델"};
        String[][] attacks={{"punch","타격"},{"smash","강타"},{"burst","연타"}};
        final Map<String,String[]> BATTLE=new LinkedHashMap<>();
        final Map<String,String[]> TALK=new LinkedHashMap<>();

        Game(){ super(MainActivity.this); p.setTypeface(Typeface.DEFAULT_BOLD); setLayerType(View.LAYER_TYPE_SOFTWARE,null); loadAssets(); seedText(); spawn(); }
        void loadAssets(){
            for(int i=0;i<5;i++) try{ stage[i]=BitmapFactory.decodeStream(getAssets().open("s"+i+".jpg")); }catch(Exception ignored){}
            AudioAttributes aa=new AudioAttributes.Builder().setUsage(AudioAttributes.USAGE_GAME).setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION).build();
            pool=new SoundPool.Builder().setMaxStreams(8).setAudioAttributes(aa).build();
            for(String n:new String[]{"hit","sparkle","win","hurt","stair"}) try(AssetFileDescriptor afd=getAssets().openFd(n+".wav")){ snd.put(n,pool.load(afd,1)); }catch(Exception ignored){}
        }
        void releaseAudio(){ if(pool!=null) pool.release(); }
        void play(String n){ Integer id=snd.get(n); if(id!=null){float v=n.equals("sparkle")?.55f:n.equals("stair")?1f:.8f;pool.play(id,v,v,1,0,1f);} }
        void seedText(){
            BATTLE.put("칭찬",new String[]{"칼끝이 예리하군. 인정해.","그 한 수, 나쁘지 않아.","힘쓰는 모습… 조금 봐 줄 만해.","제대로 맞았다. 잘했어.","숨이 거칠어진 건 네 탓이야. 칭찬이야.","그렇게 밀어붙이다니, 제법인걸.","손목이 단단하네. 훈련했구나.","지금 그 일격은 기억할게.","날 흔든 건 오랜만이야.","계속해. 그 정도는 받아 줄게."});
            BATTLE.put("미움",new String[]{"역겨워. 가까이 오지 마.","그 눈빛, 참을 수가 없어.","너 같은 놈에게 베일 줄이야.","싫어. 손 치워. 검만 대.","얼굴도 보기 싫어.","왜 나를 그런 눈으로 봐.","더럽혀진 기분이야.","네 숨소리가 싫어.","꺼져. 이 층에서 당장.","미워. 정말 미워."});
            BATTLE.put("조롱",new String[]{"그게 전부야? 초라하네.","손이 떨리는데?","고작 그걸로 나를 이기겠다고?","숨부터 고르지 그래.","방패가 웃기네.","발밑이 헤매고 있어.","검이 가벼워. 장난감이냐.","그렇게 헐떡이면 내가 미안해지잖아.","한 수 접어 줄까?","아직 옷도 안 젖었는데."});
            BATTLE.put("고통",new String[]{"윽… 거긴.","하아… 아팠어.","숨이 막혀.","무릎이 풀려.","잠깐… 숨만.","검이 뼈를 스쳤어.","으응… 깊게 들어왔다.","시려. 너무 시려.","다리가 안 모여.","눈앞이 하얘."});
            BATTLE.put("자신감",new String[]{"이 정도는 예고편이야.","탑의 주인이 나다.","네가 먼저 쓰러질 거야.","숨 한 번 고르고 다시 와.","내 검이 더 길어.","층이 올라갈수록 너는 작아져.","아직 외투도 안 벗었어.","발끝으로도 막아.","네 체력, 내가 세고 있어.","쓰러지려면 허락을 받아."});
            BATTLE.put("당황",new String[]{"잠깐, 거기는 아니야.","옷이… 왜 이래.","시선 옮기지 마.","방금 그건 실수야.","목소리가 이상해졌잖아.","손 위치를 못 찾겠어.","왜 심장이 이렇게.","그만 봐. 지금은 검만.","균형이… 깨졌어.","이런 식은 아니었어."});
            BATTLE.put("유혹",new String[]{"더 세게 해 봐. 해볼 테니까.","숨 소리를 들려줘.","옷이 거슬리면 베어.","가까이 오면, 한 번은 받아 줄게.","눈이 정직하네.","만지고 싶은 거 알아. 검부터.","무릎 꿇으면 봐 줄지도.","귓가에 대 봐. 뭐가 들릴지.","땀이 섞이면 그만 안 돼.","이 탑에서 그런 얼굴을 하다니."});
            BATTLE.put("위협",new String[]{"다음엔 목을 노린다.","한 발만 더 오면 베어.","네 배낭, 피로 물들 거야.","아래층으로 던져 주마.","손가락부터 자를까.","웃음 거둬. 피가 먼저야.","그 방패, 부숴 줄게.","내 이름을 기억해. 마지막에.","숨 고를 틈은 없다.","탑은 네 무덤이야."});
            BATTLE.put("체념",new String[]{"또 이 층인가.","언젠가 이렇게 되겠지.","옷이 벗겨져도… 검만 들면 돼.","지쳤어. 그래도 선다.","이쯤이면 충분해.","누가 이기든 탑은 남겠지.","손을 내려도 끝나지 않아.","숨만 붙어 있으면 된다.","마지막이어도 좋아.","네가 원하던 모습이지."});
            BATTLE.put("도발",new String[]{"더 벗겨 봐. 할 수 있으면.","고작 그 칼끝으로?","얼굴을 붉히긴. 누가 당황한 거야.","한 대 더. 기다릴게.","무릎이 먼저 꺾이겠네.","만질 셈이면 검부터 버려.","숨이 짧아. 들려.","층이 아깝다. 네가.","끝까지 가 봐. 내가 받아 줄게.","쓰러트리면, 그때 말해."});
            TALK.put("hair",new String[]{"머리… 만지지 마.","흩트리지 마.","손 치워. 전투 중이잖아."}); TALK.put("face",new String[]{"얼굴을 만지다니.","가까이 오지 마.","…시선이 거슬려."}); TALK.put("chest",new String[]{"거기 안 돼.","옷 밑으로 넣지 마.","하아… 정신 차려."}); TALK.put("thigh",new String[]{"허벅지는 그만.","다리가 가렵다고 하지 마.","손 올려. 지금."}); TALK.put("groin",new String[]{"거긴 절대 안 돼!","손 치워!!","어디서 손을…!!"}); TALK.put("boot",new String[]{"신발은 상관없어.","발끝은 봐 주지."}); TALK.put("air",new String[]{"허공을 더듬지 마.","나는 이쪽이야."});
        }
        void spawn(){long m=1L<<Math.min(floor-1,20);emax=(int)Math.min(500000000,40*m);ehp=emax;eatk=(int)Math.min(500000000,8*m);edef=(int)Math.min(500000000,2*m);cleared=false;pendingChest=false;inCombat=false;}
        int stageIndex(){float q=ehp/(float)Math.max(1,emax);return q<=0?4:q<=.25?3:q<=.5?2:q<=.75?1:0;}
        void txt(Canvas c,String s,float x,float y,float size,int col){p.setTextSize(size);p.setColor(col);p.setStyle(Paint.Style.FILL);c.drawText(s,x,y,p);} void box(Canvas c,float l,float t,float rr,float bb,int col,float rad){p.setStyle(Paint.Style.FILL);p.setColor(col);c.drawRoundRect(l,t,rr,bb,rad,rad,p);}
        RectF coverRect(Bitmap bm,float w,float h){float sc=Math.max(w/bm.getWidth(),h/bm.getHeight());float ww=bm.getWidth()*sc,hh=bm.getHeight()*sc;return new RectF((w-ww)/2,(h-hh)/2,(w+ww)/2,(h+hh)/2);} RectF containRect(Bitmap bm,RectF a){float sc=Math.min(a.width()/bm.getWidth(),a.height()/bm.getHeight());float ww=bm.getWidth()*sc,hh=bm.getHeight()*sc;return new RectF(a.centerX()-ww/2,a.centerY()-hh/2,a.centerX()+ww/2,a.centerY()+hh/2);}
        @Override protected void onDraw(Canvas c){super.onDraw(c);long now=System.currentTimeMillis();int w=getWidth(),h=getHeight();c.drawColor(0xff120e12);if(inCombat){c.save();if(now<fxUntil){float q=(fxUntil-now)/500f,amp=10f*q;c.translate((r.nextFloat()-.5f)*amp,(r.nextFloat()-.5f)*amp);}Bitmap bm=stage[stageIndex()];if(bm!=null)c.drawBitmap(bm,null,coverRect(bm,w,h),p);drawAttackFx(c,w,h,now);c.restore();drawBubble(c,w,now);if(now<fxUntil||now<bubbleUntil||now<flashUntil)postInvalidateDelayed(16);return;}if(pendingChest){drawChest(c,w,h);return;}drawNormal(c,w,h,now);}
        void drawNormal(Canvas c,int w,int h,long now){txt(c,"X탑",24,52,38,Color.WHITE);txt(c,"FLOOR "+floor,24,82,18,0xffcbaab4);txt(c,names[(floor-1)%names.length],24,118,24,Color.WHITE);txt(c,"체력 "+ehp+" / "+emax,24,145,15,0xffc9a8b1);box(c,24,158,w-24,166,0xff2a1a20,99);float pct=ehp/(float)Math.max(1,emax);box(c,24,158,24+(w-48)*Math.max(0,pct),166,0xffd36a7a,99);RectF a=new RectF(0,176,w,h-170);Bitmap bm=stage[stageIndex()];if(bm!=null)c.drawBitmap(bm,null,containRect(bm,a),p);box(c,20,h-156,w-20,h-90,0xff7c4857,18);txt(c,"그녀를 베다",44,h-114,24,Color.WHITE);float gap=8,bw=(w-40-gap*3)/4f;String[] bs={"다시","↓","↑","기타"};for(int i=0;i<4;i++){float x=20+i*(bw+gap);box(c,x,h-78,x+bw,h-26,0xff271a20,13);txt(c,bs[i],x+14,h-44,16,0xffead9df);}txt(c,log,22,h-6,12,0xff9a7b85);drawBubble(c,w,now);}
        void drawChest(Canvas c,int w,int h){c.drawColor(0xff1a1210);txt(c,"전투 승리",w/2f-70,h*.22f,28,0xffe8d0a8);float L=w*.25f,R=w*.75f,T=h*.36f,B=h*.55f;box(c,L,T,R,B,0xff8a5a28,8);box(c,L+8,T+12,R-8,B-18,0xff5a3514,5);box(c,w*.47f,h*.42f,w*.53f,h*.48f,0xffe8d0a8,99);box(c,w*.18f,h*.68f,w*.82f,h*.77f,0xff7c4857,18);txt(c,"상자 열기",w*.35f,h*.738f,25,Color.WHITE);}
        void drawAttackFx(Canvas c,int w,int h,long now){if(now>=fxUntil)return;float t=1f-(fxUntil-now)/500f;t=Math.max(0,Math.min(1,t));float cx=w*.5f,cy=h*.5f;if(fxKind.equals("punch")){p.setStyle(Paint.Style.FILL);p.setColor(0x99ffd0d8);c.drawCircle(cx,cy,35+65*t,p);}else if(fxKind.equals("smash")){p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(12);p.setColor(0xaafff0b0);c.drawCircle(cx,cy,35+130*t,p);}else{p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(6);p.setColor(0xccffffff);for(int i=0;i<3;i++)c.drawCircle(cx,cy,25+i*28+120*t,p);}txt(c,atkName,w*.5f-32,h*.14f,26,0xffffe08a);txt(c,"-"+lastDeal,w*.52f,h*.42f,34,0xffffe7a8);if(now<flashUntil){p.setColor(0x66fff5de);p.setStyle(Paint.Style.FILL);c.drawRect(0,0,w,h,p);}}
        void drawBubble(Canvas c,int w,long now){if(now>=bubbleUntil||bubble.length()==0)return;float l=w*.08f,rr=w*.92f,t=26,b=112;boolean mad=bubble.startsWith("!");String tx=mad?bubble.substring(1):bubble;box(c,l,t,rr,b,mad?0xff3a1418:0xfffff6ea,16);txt(c,tx,l+16,72,16,mad?0xffffd6dc:0xff3a2428);}
        void startCombat(){if(ehp<=0||pendingChest)return;inCombat=true;doAttack();}
        void doAttack(){if(ehp<=0||pendingChest)return;if(!inCombat)inCombat=true;String[] k=attacks[r.nextInt(attacks.length)];int deal=Math.max(1,atk-edef),taken=Math.max(1,eatk-def);ehp=Math.max(0,ehp-deal);atkName=k[1];fxKind=k[0];lastDeal=deal;long now=System.currentTimeMillis();fxUntil=now+500;flashUntil=now+220;play("hit");speakBattle();if(ehp<=0){cleared=true;maxFloor=Math.max(maxFloor,floor+1);play("win");inCombat=false;pendingChest=true;log=names[(floor-1)%names.length]+"이(가) 쓰러졌습니다. 상자를 여십시오.";invalidate();return;}hp=Math.max(0,hp-taken);play("hurt");if(hp<=0){hp=maxHp;floor=1;spawn();log="쓰러졌다. 1층으로 돌아갑니다.";}invalidate();}
        void speakBattle(){float pct=ehp/(float)Math.max(1,emax)*100f;String[] pool=pct>80?new String[]{"자신감","조롱","도발","칭찬"}:pct>60?new String[]{"칭찬","유혹","당황","도발"}:pct>40?new String[]{"당황","유혹","고통","미움"}:pct>20?new String[]{"고통","미움","위협","체념"}:new String[]{"체념","고통","위협","미움"};String st=pool[r.nextInt(pool.length)],tx=BATTLE.get(st)[r.nextInt(BATTLE.get(st).length)];bubble=((st.equals("미움")||st.equals("위협"))?"!":"")+tx;bubbleUntil=System.currentTimeMillis()+1800;log=names[(floor-1)%names.length]+" ("+st+") : "+tx;}
        String zone(float x,float y){float nx=x/getWidth(),ny=y/getHeight();if(nx<.18||nx>.82)return"air";if(ny<.16)return"hair";if(ny<.30)return"face";if(ny<.48)return"chest";if(ny<.58&&nx>.38&&nx<.62)return"groin";if(ny<.78)return"thigh";return"boot";} void touchHer(float x,float y){String z=zone(x,y);String[] a=TALK.get(z);String tx=a[r.nextInt(a.length)];bubble=(z.equals("groin")?"!":"")+tx;bubbleUntil=System.currentTimeMillis()+1600;play(z.equals("groin")?"hurt":"sparkle");log=names[(floor-1)%names.length]+" : "+tx;invalidate();}
        void openChest(){pendingChest=false;log="상자를 열었습니다. 룰렛/전리품 화면으로 이동합니다.";invalidate();}
        @Override public boolean onTouchEvent(MotionEvent e){if(e.getAction()!=MotionEvent.ACTION_DOWN)return true;float x=e.getX(),y=e.getY();int h=getHeight();if(inCombat){doAttack();return true;}if(pendingChest){if(y>h*.63f)openChest();return true;}if(y>h-165&&y<h-84){startCombat();return true;}if(cleared){touchHer(x,y);return true;}touchHer(x,y);return true;}
    }
}
