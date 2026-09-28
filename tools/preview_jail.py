#!/usr/bin/env python3
"""Review the 11.69 jail layout with existing portraits; NOT a Unity capture."""
import io,json,zipfile
from pathlib import Path
import fitz
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1]
FONT=fitz.Font('korea').buffer
GOLD='#FFD178'; PINK='#FF7BAB'; MUTED='#9EB0CC'; WHITE='#F5F7FF'
APK=ROOT/'unity/XTapUnity/Assets/StreamingAssets'
z=zipfile.ZipFile(io.BytesIO((APK/'xtop_source.part1').read_bytes()+(APK/'xtop_source.part2').read_bytes()))
chars=json.loads((ROOT/'unity/XTapUnity/Assets/Resources/XTapDialogue/character_dialogues.json').read_text())['characters']
def render(height,points,owned=True):
    w=1080;top=48;bottom=40;side=32;inner=w-2*side
    im=Image.new('RGB',(w,height),'#05060B');d=ImageDraw.Draw(im)
    def rect(x,y,width,h,c): d.rectangle((x,y,x+width,y+h),fill=c)
    def text(x,y,s,size=36,c=WHITE,anchor='lt'):
        for row,line in enumerate(s.split('\n')):
            d.text((x,y+row*(size+8)),line,font=ImageFont.truetype(io.BytesIO(FONT),size),fill=c,anchor=anchor)
    text(side,top+10,'감옥 · 교감',66,GOLD);rect(868,top,180,88,'#192130');text(958,top+22,'닫기',40,GOLD,'mt')
    text(side,top+108,'포획 2/10명 · 오늘 대화 가능한 캐릭터 '+('1명' if points else '2명'),34,MUTED)
    for i in range(5):
        x=side+i*238;rect(x,top+170,226,144,'#452638' if i==0 else '#121723')
        name=chars[i]['name'].split()[0]
        s=f'{i+1}층 · {name}\n호감도 {points if i==0 else 0}\n'+('대화 보상 완료' if points and i==0 else '대화 +1 가능') if i<2 else f'{i+1}층\n미포획\n포획 후 교감'
        text(x+113,top+180,s,30,GOLD if i<2 else MUTED,'mt')
    heroTop=top+332;a=height-bottom-626;hh=a-heroTop-18
    rect(side,heroTop,inner,hh,'#0B0E16')
    if owned:
        portrait=Image.open(io.BytesIO(z.read('assets/f1_cap.jpg'))).convert('RGB')
        scale=max(inner/portrait.width,(hh-316)/portrait.height)
        portrait=portrait.resize((round(portrait.width*scale),round(portrait.height*scale)),Image.Resampling.LANCZOS)
        left=(portrait.width-inner)//2
        portrait=portrait.crop((left,0,left+inner,hh-316))
        im.paste(portrait,(side,heroTop+126))
    rect(side,heroTop,inner,126,'#05060A')
    text(side+36,heroTop+14,'1층 · 레이나 발크로프트' if owned else '아직 포획된 캐릭터가 없습니다',46,GOLD)
    text(side+36,heroTop+80,'캐릭터를 터치하면 대화합니다',30,MUTED)
    sy=heroTop+hh-162-hh*.025
    rect(side+inner*.025,sy,inner*.95,162,'#070910')
    text(side+60,sy+50,'꾸준히 찾아오는 끈기는 인정하지.' if points else '왔나. 오늘은 무슨 이야기지?',42)
    rect(side,a,inner,170,'#160B13');text(side+25,a+12,'호감도 '+str(points),46,PINK)
    text(side+inner-25,a+24,'전용 가방 '+str(24+points//100)+'칸',37,GOLD,'rt')
    rect(side+25,a+85,inner-50,16,'#331F33');rect(side+25,a+85,(inner-50)*(points%100)/100,16,PINK)
    text(side+25,a+115,f'가방 +1칸까지 호감도 {100-points%100} · 누적 확장 +{points//100}칸',32,MUTED)
    rect(side,a+186,inner,142,'#090E16');text(side+25,a+203,'이 가방 장비 · 전체 수식어 ×2 적용',33,GOLD)
    text(side+25,a+259,'공격 +682   방어 +210   체력 +188',40)
    rect(side,a+346,inner,112,'#61213D');text(w/2,a+380,'다시 대화하기 · 오늘 +1 지급 완료' if points else '오늘의 대화 · 호감도 +1',43,GOLD,'mt')
    text(w/2,a+474,'캐릭터마다 하루 한 번 +1 · 한국 시간 00:00 갱신',29,MUTED,'mt')
    rect(side,a+530,inner*.57,96,'#292013');rect(side+inner*.59,a+530,inner*.41,96,'#172438')
    text(side+inner*.285,a+560,f'전용 가방 · 12/{24+points//100}칸',39,GOLD,'mt');text(side+inner*.795,a+561,'X SIGIL BEAT',34,GOLD,'mt')
    return im
out=ROOT/'unity/jail-review-11.69';out.mkdir(exist_ok=True)
canvas=Image.new('RGB',(1200,980),'#0F1320');d=ImageDraw.Draw(canvas)
d.text((26,12),'11.69 감옥 UI 배치 시뮬레이션 · Unity 캡처 아님',font=ImageFont.truetype(io.BytesIO(FONT),27),fill=GOLD)
for i,(height,score,label) in enumerate([(1920,0,'9:16 · 대화 전'),(2340,99,'긴 화면 · 호감도 99'),(2340,100,'100 달성 · 가방 25칸')]):
    frame=render(height,score);frame.thumbnail((380,855));canvas.paste(frame,(i*400+(400-frame.width)//2,90))
    d.text((i*400+22,55),label,font=ImageFont.truetype(io.BytesIO(FONT),22),fill=WHITE)
canvas.save(out/'jail-layout-design.jpg',quality=92)
print(out/'jail-layout-design.jpg')
