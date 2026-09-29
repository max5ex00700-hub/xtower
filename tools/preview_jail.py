#!/usr/bin/env python3
"""Render the 11.77 jail design using real game portraits. NOT a Unity capture.
Geometry follows XTapJail.ApplyLayout; values below are labelled sample states.
Run from any directory: python tools/preview_jail.py [output-directory]
"""
import io,json,math,sys,zipfile
from pathlib import Path
import fitz
import numpy as np
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1]
JAIL_SOURCE=(ROOT/'unity/XTapUnity/Assets/Scripts/XTapJail.cs').read_text()
import re
def source_array(name):
    match=re.search(name+r' = \{([^}]+)\}',JAIL_SOURCE)
    return [float(v.strip().rstrip('f')) for v in match[1].split(',')]
OFFSETS=source_array('PortraitTopOffsets');FACE_CENTERS=source_array('FaceCentersFromTop')
FONT=fitz.Font('korea').buffer
GOLD=(227,191,125);PINK=(237,135,166);MUTED=(163,173,191);WHITE=(250,243,230)
APK=ROOT/'unity/XTapUnity/Assets/StreamingAssets'
archive=zipfile.ZipFile(io.BytesIO((APK/'xtop_source.part1').read_bytes()+(APK/'xtop_source.part2').read_bytes()))
characters=json.loads((ROOT/'unity/XTapUnity/Assets/Resources/XTapDialogue/character_dialogues.json').read_text())['characters']
portraits={n:Image.open(io.BytesIO(archive.read(f'assets/f{n}_cap.jpg'))).convert('RGB') for n in range(1,11)}
fonts={}
def font(size):
    if size not in fonts:fonts[size]=ImageFont.truetype(io.BytesIO(FONT),size)
    return fonts[size]
def render(height=1920,points=37,char=1,owned=True,ready=True):
    w=1080;top=48;bottom=40;side=34;inner=w-2*side
    h=max(1920,top+bottom+1780,height)
    reset=h-bottom-40;actions=reset-132;talk=actions-138;stats=talk-140;bond=stats-196;speech=bond-190;name=speech-156
    hero_h=round(bond+160)
    im=Image.new('RGBA',(w,h),(5,6,9,255))
    def fill(box,color):
        layer=Image.new('RGBA',im.size);ImageDraw.Draw(layer).rectangle(box,fill=color);im.alpha_composite(layer)
    def grad(box,topc,botc):
        x,y,r,b=map(round,box);n=b-y
        arr=np.linspace(topc,botc,n).astype('uint8')[:,None,:]
        im.alpha_composite(Image.fromarray(np.repeat(arr,r-x,axis=1)),(x,y))
    def text(box,s,size=36,color=WHITE,align='left',minimum=None):
        x,y,r,b=box;tw=r-x;th=b-y;minimum=minimum or round(size*.82)
        for fs in range(size,minimum-1,-1):
            f=font(fs);lines=[]
            for para in s.split('\n'):
                row=''
                for ch in para:
                    if row and f.getlength(row+ch)>tw:lines.append(row);row=ch
                    else:row+=ch
                lines.append(row)
            line_h=fs*1.12
            if len(lines)*line_h<=th:break
        if len(lines)*line_h>th+.01:raise ValueError('Text overflow: '+s)
        d=ImageDraw.Draw(im);yy=y+(th-len(lines)*line_h)/2
        for row in lines:
            xx=x if align=='left' else (r-f.getlength(row) if align=='right' else x+(tw-f.getlength(row))/2)
            d.text((xx,yy),row,font=f,fill=color,stroke_width=0,anchor='lt');yy+=line_h
    def frame(box,c=GOLD,alpha=160):
        x,y,r,b=box;cut=min(15,(b-y)*.1);inset=2
        pts=[(x+inset+cut,b-inset),(r-inset-cut,b-inset),(r-inset,b-inset-cut),(r-inset,y+inset+cut),(r-inset-cut,y+inset),(x+inset+cut,y+inset),(x+inset,y+inset+cut),(x+inset,b-inset-cut)]
        layer=Image.new('RGBA',im.size);d=ImageDraw.Draw(layer)
        d.line(pts+[pts[0]],fill=(*c,alpha),width=2)
        d.line((x+24,y+7,min(r-24,x+86),y+7),fill=(*c,alpha),width=2)
        d.line((max(x+24,r-86),b-7,r-24,b-7),fill=(*c,alpha),width=2)
        im.alpha_composite(layer)
    def surface(box,c,alpha=145):
        rgb=np.array(c[:3]);a=c[3] if len(c)>3 else 255
        grad(box,(*np.minimum(255,rgb*np.array([1.22,1.18,1.12])).astype(int),a),(*np.minimum(255,rgb*np.array([.72,.76,.84])).astype(int),a));frame(box,alpha=alpha)
    def icon(box,kind,c=GOLD):
        x,y,r,b=box;u=min(r-x,b-y);ox=(x+r-u)/2;oy=(y+b-u)/2;d=ImageDraw.Draw(im);sw=max(2,round(u*.045))
        def line(pts,closed=False):
            q=[(ox+px*u,oy+(1-py)*u) for px,py in pts]
            d.line(q+([q[0]] if closed else []),fill=c,width=sw,joint='curve')
        if kind=='gate':
            line([(.5,.97),(.83,.74),(.83,.18),(.5,.04),(.17,.18),(.17,.74)],True)
            line([(.29,.26),(.29,.66),(.5,.82),(.71,.66),(.71,.26)])
            line([(.4,.29),(.4,.63)]);line([(.6,.29),(.6,.63)]);line([(.26,.43),(.74,.43)])
        elif kind=='heart':
            pts=[]
            for i in range(41):
                a=i/40*math.pi*2;pts.append((.5+16*math.sin(a)**3/38,.55+(13*math.cos(a)-5*math.cos(2*a)-2*math.cos(3*a)-math.cos(4*a))/38))
            line(pts)
        elif kind=='bag':
            line([(.18,.14),(.82,.14),(.76,.71),(.24,.71)],True);line([(.35,.65),(.35,.85),(.65,.85),(.65,.65)]);line([(.3,.46),(.7,.46)]);line([(.5,.35),(.5,.56)])
        elif kind=='rune':
            line([(.5,.95),(.85,.5),(.5,.05),(.15,.5)],True);line([(.33,.33),(.67,.67)]);line([(.33,.67),(.67,.33)])
        else:
            line([(.19,.12),(.81,.12),(.81,.57),(.19,.57)],True);line([(.31,.57),(.31,.77),(.4,.87),(.6,.87),(.69,.77),(.69,.57)]);line([(.5,.26),(.5,.43)])
    def anchor(box,ax,ay,bx,by):
        x,y,r,b=box;return (x+ax*(r-x),y+(1-by)*(b-y),x+bx*(r-x),y+(1-ay)*(b-y))
    # Same full-width top-aligned EnvelopeParent portrait as the runtime.
    fill((0,0,w,hero_h),(6,9,14,255))
    if owned:
        portrait_top=top+252+OFFSETS[char-1]
        portrait=portraits[char];scale=max(w/portrait.width,(hero_h-portrait_top)/portrait.height)
        image=portrait.resize((round(portrait.width*scale),round(portrait.height*scale)),Image.Resampling.LANCZOS)
        left=(image.width-w)//2;image=image.crop((left,0,left+w,round(hero_h-portrait_top)))
        im.alpha_composite(image.convert('RGBA'),(0,round(portrait_top)))
    grad((0,0,w,hero_h*.33),(4,6,9,255),(0,0,0,0))
    grad((0,hero_h*.56,w,hero_h),(0,0,0,0),(5,6,9,255))
    frame((w*.028,hero_h*.28,w*.972,hero_h*.98),alpha=61)
    if not owned:
        empty_top=(top+368+name-360)*.5;empty=(side,empty_top,w-side,empty_top+360)
        icon(anchor(empty,.39,.46,.61,.98),'gate')
        text(anchor(empty,0,.22,1,.45),'아직 비어 있는 감옥',56,GOLD,'center')
        text(anchor(empty,0,0,1,.20),'전투 후 블럭 머신에서 첫 인연을 만나세요',32,MUTED,'center')
    icon((side,top+3,side+70,top+85),'gate')
    text((side+92,top-4,w-side-188,top+96),'감옥',72)
    surface((w-side-154,top+4,w-side,top+84),(17,19,24,242))
    text((w-side-154,top+4,w-side,top+84),'닫기',34,GOLD,'center')
    text((side,top+96,w-side,top+140),'포획 3 / 10명    ·    오늘 교감 가능 2명' if owned else '포획 0 / 10명    ·    오늘 교감 가능 0명',32,MUTED)
    fill((side,top+153,w-side,top+154),(227,191,125,82))
    text((side,top+160,w-side,top+196),'캐릭터 선택  /  좌우로 넘기기',26,MUTED)
    # Clip strip at the same viewport boundary as the runtime.
    before=im.copy()
    owned_ids={1,2,char if char>2 else 5} if owned else set()
    offset=max(0,min(10*188-12-inner,(char-1)*188-(inner-176)*.5)) if owned else 0
    for i in range(10):
        ident=i+1;x=round(side+i*188-offset);y=top+202;r=x+176;b=y+166;is_owned=ident in owned_ids
        if x>=w-side or r<=side:continue
        fill((x,y,r,b),(38,31,24,250) if ident==char and owned else (9,11,16,245))
        fill((x+7,y+7,x+169,y+98),(15,19,24,255))
        if is_owned:
            face=portraits[ident];crop_h=round(face.width*91/162)
            crop_y=round(max(0,min(face.height-crop_h,face.height*FACE_CENTERS[ident-1]-crop_h*.5)))
            im.alpha_composite(face.crop((0,crop_y,face.width,crop_y+crop_h)).resize((162,91),Image.Resampling.LANCZOS).convert('RGBA'),(x+7,y+7))
        else:icon((x+64,y+22,x+112,y+77),'lock',(97,110,130))
        text((x+15,y+8,x+67,y+40),f'{ident:02}',25,GOLD)
        text((x+4,y+100,r-4,y+135),characters[i]['name'].split()[0] if is_owned else '미포획',30,GOLD if char==ident and owned else WHITE,'center')
        text((x+4,y+134,r-4,y+163),('교감 +1 가능' if ready or ident!=char else '오늘 교감 완료') if is_owned else '포획 후 해금',23,PINK if is_owned else MUTED,'center')
        frame((x,y,r,b),alpha=255 if owned and char==ident else (89 if is_owned else 38))
    im.paste(before.crop((w-side,top+202,w,top+368)),(w-side,top+202))
    im.paste(before.crop((0,top+202,side,top+368)),(0,top+202))
    identity=(side+8,name,w-side-8,name+140)
    grad(anchor(identity,-.012,-.02,1.012,1.02),(4,6,9,107),(4,6,9,222))
    text(anchor(identity,.005,.70,.68,1),f'{char:02}층  /  포획 캐릭터' if owned else '교감의 기록',31,GOLD)
    text(anchor(identity,.62,.70,.995,1),('오늘 +1 가능' if ready else '오늘 +1 완료') if owned else '미포획',30,PINK,'right')
    text(anchor(identity,0,.24,1,.72),characters[char-1]['name'] if owned else '첫 만남을 기다리며',62)
    text(anchor(identity,.005,0,.995,.25),'캐릭터를 터치해 이야기를 나누세요' if owned else '캐릭터를 포획하고 다시 찾아오세요',28,(199,207,217))
    box=(side,speech,w-side,speech+174);surface(box,(9,11,15,247),133)
    text(anchor(box,.035,.72,.965,.95),characters[char-1]['name'].split()[0]+'의 이야기' if owned else '감옥 이용 안내',26,GOLD)
    lines={1:'왔나. 오늘은 무슨 이야기지?',2:'어머, 오늘도 나 보러 온 거야?',5:'왔네. 앉아.'}
    phrase=(lines.get(char,'오늘은 어떤 이야기를 할까요?') if ready else '꾸준히 찾아오는 끈기는 인정하지.') if owned else '블럭 머신에서 0 선택 후 포획 확률 1%\n포획하면 대화와 전용 가방이 열립니다.'
    text(anchor(box,.035,.09,.965,.73),phrase,42,(245,242,240))
    capacity=24+points//100
    box=(side,bond,w-side,bond+180);surface(box,(26,13,18,250),140)
    icon((side+24,bond+24,side+68,bond+68),'heart',PINK)
    text(anchor(box,.087,.60,.23,.94),'호감도',30,(212,173,186))
    text(anchor(box,.23,.57,.53,.97),str(points),56,PINK)
    text(anchor(box,.53,.63,.97,.94),f'가방 {capacity}칸 · 확장 +{points//100}',36,GOLD,'right')
    fill(anchor(box,.035,.37,.965,.45),(6,5,8,255))
    fill(anchor(box,.035,.37,.035+.93*(points%100)/100,.45),PINK)
    text(anchor(box,.035,.02,.73,.30),f'가방 +1칸까지 {100-points%100} 남음' if owned else '호감도 100마다 전용 가방 +1칸',29,MUTED)
    text(anchor(box,.73,.02,.97,.30),f'{points%100} / 100',28,PINK,'right')
    box=(side,stats,w-side,stats+124);surface(box,(8,12,17,250),77)
    text(anchor(box,.025,.69,.48,.98),'전용 장비 능력치',26,MUTED)
    text(anchor(box,.48,.69,.975,.98),'수식어 ×2 적용' if owned else '수식어 ×1 적용',27,GOLD,'right')
    for pos,label,value,c in [(.025,'공격','+682',(255,189,145)),(.355,'방어','+210',(145,196,232)),(.685,'체력','+188',(240,148,163))]:
        text(anchor(box,pos,.09,pos+.10,.64),label,28,MUTED)
        text(anchor(box,pos+.10,.09,pos+.285,.64),value if owned else '+0',43,c,'right')
    box=(side,talk,w-side,talk+118);surface(box,(99,51,56) if owned else (48,24,28),148)
    icon((side+25,talk+38,side+67,talk+80),'heart',PINK)
    text((side+82,talk+6,w-side-18,talk+112),('오늘의 대화   ·   호감도 +1' if ready else '다시 이야기 나누기') if owned else '포획 후 대화 가능',43,(247,230,194),'center')
    bw=(inner-18)*.55;gx=side+bw+18
    for x,r,kind,color,caption,size in [(side,side+bw,'bag',(31,26,19),f'전용 가방\n{12 if owned else 0}/{capacity}칸',36),(gx,w-side,'rune',(14,22,32),'미니게임\n'+('X SIGIL BEAT' if owned and char==1 else '준비 중'),32)]:
        if not owned or (kind=='rune' and char!=1):color=tuple(round(v*.48) for v in color)
        box=(round(x),actions,round(r),actions+112);surface(box,color,148)
        icon((x+25,actions+35,x+67,actions+77),kind,GOLD if kind=='bag' else (150,199,240))
        text((x+82,actions+6,r-18,actions+106),caption,size,(247,230,194),'center')
    text((side,reset,w-side,reset+40),'하루 첫 대화 +1  ·  한국 시간 00:00 갱신',27,MUTED,'center')
    return im.convert('RGB'),{'height':h,'selector_bottom':top+368,'name_top':name,'speech_top':speech,'bond_top':bond,'stats_top':stats,'talk_top':talk,'actions_top':actions,'reset_top':reset}

def main():
    out=Path(sys.argv[1]) if len(sys.argv)>1 else ROOT/'unity/jail-review-11.77'
    out.mkdir(parents=True,exist_ok=True)
    cases=[('jail-1920.jpg',1920,37,1,True,True,'9:16 · 대화 전'),('jail-2340.jpg',2340,100,1,True,False,'긴 화면 · 호감도 100'),('jail-empty.jpg',1920,0,1,False,False,'미포획 상태'),('jail-character-5.jpg',1920,99,5,True,True,'다른 캐릭터 · 호감도 99')]
    board=Image.new('RGB',(1440,860),(8,11,17));d=ImageDraw.Draw(board)
    d.text((22,14),'11.77 감옥 UI · 실제 에셋 기반 배치 미리보기 / Unity 실행 캡처 아님',font=font(25),fill=GOLD)
    layouts=[]
    for i,(filename,height,points,char,owned,ready,label) in enumerate(cases):
        im,layout=render(height,points,char,owned,ready)
        if filename=='jail-1920.jpg':im.save(out/filename,quality=86,optimize=True)
        preview=im.copy();preview.thumbnail((342,742));board.paste(preview,(i*360+(360-preview.width)//2,97))
        d.text((i*360+12,62),label,font=font(23),fill=WHITE);layouts.append(layout)
    board.save(out/'jail-layout-design.jpg',quality=87,optimize=True)
    (out/'layout-review.json').write_text(json.dumps({'note':'Design approximation, not Unity rendering. Stats/ownership are sample data.','layouts':layouts},indent=2))
    print(out/'jail-layout-design.jpg')
if __name__=='__main__':main()
