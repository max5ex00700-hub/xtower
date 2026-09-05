package com.xtower.game;

import android.app.Activity;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.graphics.*;
import android.graphics.drawable.*;
import android.view.*;
import android.media.*;
import android.content.*;
import java.util.*;
import org.json.*;

public class MainActivity extends Activity {
    GameView game;
    @Override public void onCreate(Bundle b){
        super.onCreate(b);
        getWindow().setFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN, WindowManager.LayoutParams.FLAG_FULLSCREEN);
        getWindow().getDecorView().setSystemUiVisibility(
            View.SYSTEM_UI_FLAG_FULLSCREEN|View.SYSTEM_UI_FLAG_HIDE_NAVIGATION|View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY);
        game=new GameView(this); setContentView(game);
    }
    @Override protected void onDestroy(){ super.onDestroy(); if(game!=null)game.releaseAudio(); }

    static class Item { String name; int mod; Item(String n,int m){name=n;mod=m;} }

    class GameView extends View {
        final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG); final Random rng=new Random(); final Handler handler=new Handler(Looper.getMainLooper());
        final int BG=0xff160e12, HUD=0xff1a1216, TXT=0xfff3e6ea, MUTED=0xffc9a8b1, ACC=0xffc45c6a;
        Bitmap[] body=new Bitmap[5]; SoundPool sound; int sndHit,sndHurt,sndWin,sndSparkle;
        int floor=1,maxUnlock=1,curHp=100,baseHp=100,baseAtk=5,baseDef=3;
        Enemy foe; boolean cleared=false,inCombat=false,pendingChest=false; long bubbleUntil=0,fxUntil=0,rouletteUntil=0;
        String bubbleText="", bubbleStyle="", atkName=""; int fxKind=0,fxDeal=0; float fxX=.5f,fxY=.46f; int rouletteMod=0;
        ArrayList<Item> stash=new ArrayList<>(); HashMap<Integer,String> jail=new HashMap<>();
        RectF fightBtn=new RectF(), refightBtn=new RectF(), downBtn=new RectF(), upBtn=new RectF(), bagBtn=new RectF(), forgeBtn=new RectF(), jailBtn=new RectF();
        final String[] foeNames={"아이린","이리스","마를렌","노에","카렌","비비안","루나","에델"};
        final String[] itemNames={"입맞춤 회복약","옷 찢는 단도","치마 가르개","가슴받이 방패","쇠코르셋","손으로 먹이는 빵","심문 등잔","금기 서책","밀어붙이는 창","눈가리개 투구","찢어진 팬티","끈 브라","이름표 목줄","은 수갑","결박 밧줄","재갈","실크 안대","발목 사슬","발목 고리","가죽 하네스"};

        final LinkedHashMap<String,String[]> BATTLE=new LinkedHashMap<>();
        final HashMap<String,String[]> TALK=new HashMap<>();

        class Enemy { String name; int hp,maxHp,str,def; }

        GameView(Context c){ super(c); setLayerType(View.LAYER_TYPE_SOFTWARE,null); initText(); loadImages(); loadAudio(); restore(); spawnFoe(); }

        void initText(){
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
            TALK.put("hair",new String[]{"머리… 만지지 마.","흩트리지 마.","손 치워. 전투 중이잖아."});
            TALK.put("face",new String[]{"얼굴을 만지다니.","가까이 오지 마.","…시선이 거슬려."});
            TALK.put("chest",new String[]{"거기 안 돼.","옷 밑으로 넣지 마.","하아… 정신 차려."});
            TALK.put("thigh",new String[]{"허벅지는 그만.","다리가 가렵다고 하지 마.","손 올려. 지금."});
            TALK.put("groin",new String[]{"거긴 절대 안 돼!","손 치워!!","어디서 손을…!!"});
            TALK.put("boot",new String[]{"신발은 상관없어.","발끝은 봐 주지."});
            TALK.put("air",new String[]{"허공을 더듬지 마.","나는 이쪽이야."});
        }

        void loadImages(){
            body[0]=BitmapFactory.decodeResource(getResources(),R.drawable.s0); body[1]=BitmapFactory.decodeResource(getResources(),R.drawable.s1);
            body[2]=BitmapFactory.decodeResource(getResources(),R.drawable.s2); body[3]=BitmapFactory.decodeResource(getResources(),R.drawable.s3); body[4]=BitmapFactory.decodeResource(getResources(),R.drawable.s4);
        }
        void loadAudio(){
            AudioAttributes aa=new AudioAttributes.Builder().setUsage(AudioAttributes.USAGE_GAME).setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION).build();
            sound=new SoundPool.Builder().setMaxStreams(8).setAudioAttributes(aa).build();
            sndHit=sound.load(MainActivity.this,R.raw.hit,1); sndHurt=sound.load(MainActivity.this,R.raw.hurt,1); sndWin=sound.load(MainActivity.this,R.raw.win,1); sndSparkle=sound.load(MainActivity.this,R.raw.sparkle,1);
        }
        void releaseAudio(){ if(sound!=null)sound.release(); }
        void play(int id){ if(sound!=null&&id!=0)sound.play(id,1f,1f,1,0,1f); }

        void spawnFoe(){ long m=1L<<Math.min(floor-1,20); foe=new Enemy(); foe.name=foeNames[(floor-1)%foeNames.length]; foe.maxHp=(int)Math.min(500000000L,40L*m); foe.hp=foe.maxHp; foe.str=(int)Math.min(500000000L,8L*m); foe.def=(int)Math.min(500000000L,2L*m); cleared=false; pendingChest=false; inCombat=false; }
        int playerAtk(){ return baseAtk; } int playerDef(){ return baseDef; } int maxHp(){ return baseHp; }
        int stage(){ float q=foe==null?1f:foe.hp/(float)Math.max(1,foe.maxHp); return q<=0?4:q<=.25f?3:q<=.50f?2:q<=.75f?1:0; }

        @Override protected void onDraw(Canvas c){ super.onDraw(c); long now=System.currentTimeMillis(); if(inCombat)drawCombat(c,now); else if(pendingChest)drawChest(c,now); else drawNormal(c,now); if(now<bubbleUntil||now<fxUntil||now<rouletteUntil){postInvalidateOnAnimation();} }

        void drawNormal(Canvas c,long now){ int w=getWidth(),h=getHeight(); c.drawColor(BG); float hudH=108; p.setColor(HUD);c.drawRect(0,0,w,hudH,p); text(c,foe.name,18,30,22,0xfff0cfd8,true); text(c,"FLOOR "+floor+"   HP "+curHp+"/"+maxHp(),18,54,13,MUTED,false); float pct=foe.hp/(float)Math.max(1,foe.maxHp); round(c,new RectF(18,66,w-18,74),99,0xff2a1a20); p.setColor(ACC);c.drawRoundRect(new RectF(18,66,18+(w-36)*pct,74),99,99,p); text(c,"적 "+foe.hp+"/"+foe.maxHp+"   힘 "+playerAtk()+"   방어 "+playerDef(),18,95,13,0xff9a7b85,false);
            float dockH=152; RectF arena=new RectF(0,hudH,w,h-dockH); drawBitmapContain(c,body[stage()],arena,false);
            float y=h-dockH+8; fightBtn.set(18,y,w-18,y+58); button(c,fightBtn,"그녀를 베다",0xff6f3542,true); y+=68; float gap=6,bw=(w-36-gap*5)/6f; RectF[] rs={refightBtn,downBtn,upBtn,bagBtn,forgeBtn,jailBtn}; String[] labels={"다시","↓","↑","배낭","대장간","감옥"}; for(int i=0;i<6;i++){rs[i].set(18+i*(bw+gap),y,18+i*(bw+gap)+bw,y+50);button(c,rs[i],labels[i],0xff2b2026,false);} if(now<rouletteUntil)drawRouletteToast(c,now);
            if(cleared && !pendingChest && now<bubbleUntil)drawBubble(c,now,false);
        }

        void drawCombat(Canvas c,long now){ int w=getWidth(),h=getHeight(); c.drawColor(BG); int save=c.save(); if(now<fxUntil){float k=(float)((fxUntil-now)/500.0);float amp=8f*k;c.translate((rng.nextFloat()-.5f)*amp*2,(rng.nextFloat()-.5f)*amp*2);} drawBitmapContain(c,body[stage()],new RectF(0,0,w,h),true); if(now<fxUntil)drawAttackFx(c,now); if(now<bubbleUntil)drawBubble(c,now,bubbleStyle.equals("미움")||bubbleStyle.equals("위협")); c.restoreToCount(save); }

        void drawChest(Canvas c,long now){ int w=getWidth(),h=getHeight(); c.drawColor(0xff1a1210); float cx=w/2f,cy=h/2f-20; p.setStyle(Paint.Style.FILL);p.setColor(0xff5b321c);c.drawRoundRect(new RectF(cx-105,cy-70,cx+105,cy+75),18,18,p);p.setColor(0xffb1782d);c.drawRoundRect(new RectF(cx-105,cy-70,cx+105,cy-35),18,18,p);p.setColor(0xffffd27a);c.drawRect(cx-14,cy-70,cx+14,cy+75,p);p.setColor(0xff3a2014);c.drawRoundRect(new RectF(cx-28,cy-5,cx+28,cy+42),8,8,p);textCenter(c,"보상 상자",cx,cy-110,28,0xffffe5b5,true);textCenter(c,"화면을 터치해 상자를 여십시오",cx,cy+130,17,0xfff3d6b0,false); }

        void drawRouletteToast(Canvas c,long now){ int w=getWidth(); float y=122; RectF r=new RectF(22,y,w-22,y+92); round(c,r,20,0xee21181d);textCenter(c,"룰렛 결과  "+(rouletteMod>0?"+":"")+rouletteMod+"%",w/2f,y+38,22,rouletteMod>0?0xff9ae3ac:0xffff91a2,true);textCenter(c,"전리품이 주머니에 들어왔습니다",w/2f,y+68,14,0xffd5c5cc,false); }

        void drawBubble(Canvas c,long now,boolean mad){ int w=getWidth(); float l=w*.08f,r=w*.92f,t=getHeight()*.06f; p.setColor(mad?0xff3a1418:0xfffff6ea);RectF box=new RectF(l,t,r,t+92);c.drawRoundRect(box,18,18,p); if(mad){p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(2);p.setColor(0xffc45c6a);c.drawRoundRect(box,18,18,p);p.setStyle(Paint.Style.FILL);} drawWrapped(c,bubbleText,l+16,t+28,r-l-32,15,mad?0xffffd6dc:0xff3a2428); }

        void drawAttackFx(Canvas c,long now){ int w=getWidth(),h=getHeight(); float cx=fxX*w,cy=fxY*h; float age=1f-(fxUntil-now)/500f; age=Math.max(0,Math.min(1,age)); float fade=1f-age; p.setStyle(Paint.Style.STROKE);p.setStrokeCap(Paint.Cap.ROUND);p.setStrokeWidth(fxKind==1?9:6);p.setColor(((int)(fade*255)<<24)|0x00fff0d0); if(fxKind==0){c.drawCircle(cx,cy,30+80*age,p);c.drawLine(cx-80*age,cy+55*age,cx+80*age,cy-55*age,p);}else if(fxKind==1){c.drawCircle(cx,cy,40+170*age,p);c.drawCircle(cx,cy,18+90*age,p);}else{for(int i=0;i<3;i++)c.drawCircle(cx,cy,25+(70*i+120)*age,p);}p.setStyle(Paint.Style.FILL);textCenter(c,atkName,w/2f,h*.13f,28,0xffffe08a,true);textCenter(c,"-"+fxDeal,cx,cy-60*age,32,0xffffe7a8,true); }

        void doAttack(float x,float y){ if(foe==null||foe.hp<=0||pendingChest)return; inCombat=true; int deal=Math.max(1,playerAtk()-foe.def); int taken=Math.max(1,foe.str-playerDef()); foe.hp=Math.max(0,foe.hp-deal); play(sndHit); fxKind=rng.nextInt(3); atkName=fxKind==0?"타격":fxKind==1?"강타":"연타"; fxDeal=deal;fxX=x/Math.max(1f,getWidth());fxY=y/Math.max(1f,getHeight());fxUntil=System.currentTimeMillis()+500; speakBattle(); if(foe.hp<=0){cleared=true;if(floor>=maxUnlock)maxUnlock=floor+1;play(sndWin);inCombat=false;pendingChest=true;persist();invalidate();return;} curHp=Math.max(0,curHp-taken);play(sndHurt); if(curHp<=0)playerDie(); persist();invalidate(); }

        void speakBattle(){ float pct=foe.hp/(float)Math.max(1,foe.maxHp)*100f; String[] pool=pct>80?new String[]{"자신감","조롱","도발","칭찬"}:pct>60?new String[]{"칭찬","유혹","당황","도발"}:pct>40?new String[]{"당황","유혹","고통","미움"}:pct>20?new String[]{"고통","미움","위협","체념"}:new String[]{"체념","고통","위협","미움"}; bubbleStyle=pool[rng.nextInt(pool.length)];String[] a=BATTLE.get(bubbleStyle);bubbleText=a[rng.nextInt(a.length)];bubbleUntil=System.currentTimeMillis()+1800; }

        void touchHer(float x,float y){ String z=zoneOf(x,y);String[] arr=TALK.get(z);bubbleText=arr[rng.nextInt(arr.length)];bubbleStyle=z.equals("groin")?"위협":"";bubbleUntil=System.currentTimeMillis()+1600;if(z.equals("groin")){play(sndHurt);fxUntil=System.currentTimeMillis()+450;}else play(sndSparkle);invalidate(); }
        String zoneOf(float x,float y){ float nx=x/Math.max(1f,getWidth()),ny=y/Math.max(1f,getHeight());if(nx<.18||nx>.82||ny<0||ny>1)return"air";if(ny<.16)return"hair";if(ny<.30)return"face";if(ny<.48)return"chest";if(ny<.58&&nx>.38&&nx<.62)return"groin";if(ny<.78)return"thigh";return"boot"; }

        void openChest(){ int[] mods={-50,-40,-30,-20,-10,10,20,30,40,50};rouletteMod=mods[rng.nextInt(mods.length)];int n=1+rng.nextInt(2)+(floor>=4?1:0);for(int i=0;i<n;i++)stash.add(new Item(itemNames[rng.nextInt(itemNames.length)],rouletteMod));if(!jail.containsKey(floor)&&rng.nextInt(100)==0)jail.put(floor,foe.name);pendingChest=false;rouletteUntil=System.currentTimeMillis()+1600;persist();invalidate(); }

        void playerDie(){ inCombat=false;pendingChest=false;stash.clear();floor=1;curHp=maxHp();spawnFoe();play(sndHurt);persist();invalidate(); }
        void goFloor(int f){ if(f<1||f>maxUnlock)return;floor=f;curHp=maxHp();spawnFoe();persist();invalidate(); }

        @Override public boolean onTouchEvent(android.view.MotionEvent e){ if(e.getAction()!=MotionEvent.ACTION_UP)return true;float x=e.getX(),y=e.getY(); if(inCombat){doAttack(x,y);return true;} if(pendingChest){openChest();return true;} if(fightBtn.contains(x,y)&&foe.hp>0){inCombat=true;doAttack(getWidth()/2f,getHeight()/2f);return true;} if(refightBtn.contains(x,y)&&cleared){curHp=maxHp();spawnFoe();inCombat=true;doAttack(getWidth()/2f,getHeight()/2f);return true;} if(downBtn.contains(x,y)){goFloor(floor-1);return true;} if(upBtn.contains(x,y)){goFloor(floor+1);return true;} if(cleared){touchHer(x,y);return true;} return true; }

        void persist(){ try{JSONObject o=new JSONObject();o.put("floor",floor);o.put("maxUnlock",maxUnlock);o.put("curHp",curHp);getSharedPreferences("xtower",0).edit().putString("save",o.toString()).apply();}catch(Exception ignored){} }
        void restore(){ try{String s=getSharedPreferences("xtower",0).getString("save","");if(s.length()>0){JSONObject o=new JSONObject(s);floor=o.optInt("floor",1);maxUnlock=o.optInt("maxUnlock",1);curHp=o.optInt("curHp",100);}}catch(Exception ignored){} }

        void drawBitmapContain(Canvas c,Bitmap bm,RectF box,boolean cover){ if(bm==null)return;float sx=box.width()/bm.getWidth(),sy=box.height()/bm.getHeight(),sc=cover?Math.max(sx,sy):Math.min(sx,sy);float ww=bm.getWidth()*sc,hh=bm.getHeight()*sc;RectF d=new RectF(box.centerX()-ww/2,box.centerY()-hh/2,box.centerX()+ww/2,box.centerY()+hh/2);c.save();c.clipRect(box);c.drawBitmap(bm,null,d,p);c.restore(); }
        void button(Canvas c,RectF r,String s,int color,boolean big){round(c,r,18,color);textCenter(c,s,r.centerX(),r.centerY()+6,big?20:14,TXT,true);} void round(Canvas c,RectF r,float rad,int col){p.setStyle(Paint.Style.FILL);p.setColor(col);c.drawRoundRect(r,rad,rad,p);} void text(Canvas c,String s,float x,float y,float z,int col,boolean bold){p.setTypeface(bold?Typeface.DEFAULT_BOLD:Typeface.DEFAULT);p.setTextSize(z);p.setColor(col);p.setStyle(Paint.Style.FILL);c.drawText(s,x,y,p);} void textCenter(Canvas c,String s,float x,float y,float z,int col,boolean bold){p.setTypeface(bold?Typeface.DEFAULT_BOLD:Typeface.DEFAULT);p.setTextSize(z);p.setColor(col);p.setStyle(Paint.Style.FILL);c.drawText(s,x-p.measureText(s)/2,y,p);} 
        void drawWrapped(Canvas c,String s,float x,float y,float width,float size,int color){p.setTypeface(Typeface.DEFAULT_BOLD);p.setTextSize(size);p.setColor(color);String rest=s;float yy=y;while(rest.length()>0){int count=p.breakText(rest,true,width,null);if(count<=0)break;text(c,rest.substring(0,count),x,yy,size,color,true);rest=rest.substring(count);yy+=size*1.55f;}}
    }
}
