#!/usr/bin/env python3
"""Static/key-pose design preview, NOT a Unity or device recording.

Requires Pillow, numpy, PyMuPDF, ffmpeg. Uses the actual PNGs and 11.66 geometry.
"""
import argparse
import io
import math
from pathlib import Path
import subprocess
import tempfile
import wave

import fitz
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT = Path(__file__).resolve().parents[1]
RES = ROOT / 'unity/XTapUnity/Assets/Resources/XTapBlacksmithUI'
FONT = fitz.Font('korea').buffer


def label(image, text, x, y, size, fill):
    ImageDraw.Draw(image).text((x/2, y/2), text, anchor='mm', fill=fill,
        font=ImageFont.truetype(io.BytesIO(FONT), round(size/2)), stroke_width=1,
        stroke_fill='#080B14')


class Scene:
    def __init__(self, height, top_inset=0, bottom_inset=0):
        self.height = height
        self.top = max(28, top_inset+20)
        self.bottom = max(28, bottom_inset+20)
        self.contact = (0, -1)  # 2x1 cells, 52 px high, base at -53
        self.stage_top = self.top + 410
        upper = self.contact[1] + 900*.787
        result_limit = height-self.bottom-420
        self.scale = max(.45, min(1.02, (result_limit-self.stage_top-24)/(upper+458)))
        self.center_y = self.stage_top + upper*self.scale
        self.result_top = self.center_y + 458*self.scale + 24
        self.plate_height = height-self.bottom-100-self.result_top
        background = Image.open(RES/'forge_duel_background.bytes').convert('RGB')
        self.background = ImageOps.fit(background, (540, height//2), Image.Resampling.LANCZOS).convert('RGBA')
        shade = np.zeros((height//2, 540, 4), dtype=np.uint8)
        shade[:, :, 3] = (np.interp(1-np.arange(height//2)/(height//2),
            [0,.25,.6,.82,1], [.82,.55,.06,0,.32])*255).astype(np.uint8)[:,None]
        self.background.alpha_composite(Image.fromarray(shade))
        self.poses = []
        for name in ('forge_angel_poses','forge_demon_poses'):
            sheet=Image.open(RES/(name+'.bytes')).convert('RGBA')
            assert sheet.width == sheet.height*3
            frames=[]
            for i in range(3):
                frame=sheet.crop((i*sheet.height,0,(i+1)*sheet.height,sheet.height))
                assert .25 < np.mean(np.asarray(frame)[:,:,3]<16) < .9
                frames.append(frame.resize((round(450*self.scale),)*2,Image.Resampling.LANCZOS))
            self.poses.append(frames)
        anvil=Image.open(RES/'forge_anvil.bytes').convert('RGBA')
        self.anvil=anvil.resize((round(340*self.scale),round(680/3*self.scale)),Image.Resampling.LANCZOS)
        # Contact is computed from the same measured image coordinates as C#.
        self.homes=[]
        for x,y in ((.923,.746),(.102,.787)):
            self.homes.append((self.contact[0]-(x-.5)*900,self.contact[1]-(.5-y)*900))
        assert self.center_y+458*self.scale < self.result_top
        assert self.top+402 <= self.stage_top
        assert self.plate_height >= 319.9
        assert self.result_top+self.plate_height+100 <= height-self.bottom+.01

    def point(self,x,y):
        return ((540+x*self.scale)/2,(self.center_y-y*self.scale)/2)

    def paste(self,image,sprite,x,y):
        px,py=self.point(x,y)
        image.alpha_composite(sprite,(round(px-sprite.width/2),round(py-sprite.height/2)))

    def render(self,angel_pose=0,demon_pose=0,active=0,final=False,success=True,spark=0,twist=False,reveal=False):
        image=self.background.copy()
        label(image,'대장간 · 분해',540,self.top+46,68,'#FFE0A3')
        label(image,'기본 성공률 30%',540,self.top+134,56,'#FFD46E')
        label(image,'제물 가치 3  ·  반지',540,self.top+207,30,'#DCE3F5')
        order=(1,0) if active==0 else (0,1)
        for side in order:
            pose=(angel_pose,demon_pose)[side]
            self.paste(image,self.poses[side][pose],*self.homes[side])
        self.paste(image,self.anvil,0,-232)
        draw=ImageDraw.Draw(image)
        for x in range(2):
            px,py=self.point((x+.5)*52-52,-27)
            half=25*self.scale/2
            draw.rectangle((px-half,py-half,px+half,py+half),fill='#FFBD45')
            draw.rectangle((px-half*.7,py-half*.7,px+half*.7,py+half*.7),fill='#140E09')
        if spark:
            px,py=self.point(*self.contact)
            for i in range(20):
                angle=i*math.pi*2/20
                length=(70 if i%3==0 else 40)*self.scale*spark
                draw.line((px,py,px+math.cos(angle)*length,py+math.sin(angle)*length),
                    fill='#FFDD61' if active==0 else '#FF5328',width=3)
            label(image,'깡!!' if final else '깡!',540,self.center_y-(self.contact[1]+155)*self.scale,
                  round(108*self.scale),'#FFE091')
        for side in (0,1):
            if final and side != active: continue
            x=42 if side==0 else 1080-42-996*.465
            y=self.top+254; width=996*.465
            fill='#F7F2DE' if side==0 else '#33090E'
            ink='#19293B' if side==0 else '#FFE3C9'
            draw.rounded_rectangle((x/2,y/2,(x+width)/2,(y+148)/2),radius=12,
                fill=fill,outline='#C29247',width=2)
            tail=x+width*(.18 if side==0 else .82)
            draw.polygon(((tail/2-13,(y+146)/2),(tail/2,(y+178)/2),(tail/2+13,(y+146)/2)),fill=fill)
            line=('잘 만들어 볼게!' if side==0 else '어디 잘되나 보자!')
            if final: line=('빛이여, 힘을!' if active==0 else '내 차례다!')
            if reveal: line=('손이...' if active==0 else '젠장...')
            label(image,line,x+width/2,y+74,44,ink)
        y=self.result_top/2; ph=self.plate_height
        draw.rectangle((21,y,519,y+ph/2),fill='#070C15')
        draw.line((61,y,479,y),fill='#BC873F',width=1)
        phase=('천사의 마지막 일격' if active==0 else '악마의 마지막 일격') if final else ('천사의 타격' if active==0 else '악마의 타격')
        if reveal: phase='1% 반전 · '+('천사의 실수!' if active==0 else '악마의 실수!')
        label(image,phase,540,self.result_top+ph*.165,32,'#A8E3FF' if active==0 else '#FF8266')
        if final:
            result='분해 성공' if success else '분해 실패'
            reward='가방 배치칸 +1' if success else '제물 소모'
            if twist:
                result='...?!' if not reveal else ('대성공 · 보상 2배!' if success else '분해 실패')
                reward='' if not reveal else ('가방 배치칸 +2' if success else '제물 소모')
            label(image,result,540,self.result_top+ph*.47,70,'#FFE380' if success else '#FF573F')
            label(image,reward,540,self.result_top+ph*.80,40,'#E0F0FF')
        label(image,'막타 반전 각 1%',540,self.height-self.bottom-70,30,'#F0DBB3')
        label(image,'천사: 실패  ·  악마: 성공 + 보상 2배',540,self.height-self.bottom-30,30,'#F0DBB3')
        return image.convert('RGB')


def timeline(angel_final):
    steps=[]; start=.18
    for i in range(6):
        duration=.32-.02*i
        steps.append((start,duration,i%2,False)); start+=duration
    start+=.12
    steps.append((start,.58,0 if angel_final else 1,True))
    return steps,start+.58+.18+1.85


def make_video(path,angel_final):
    scene=Scene(2340,90,60); fps=30
    steps,total=timeline(angel_final)
    track=np.zeros(round((total+.2)*44100))
    for start,duration,side,final in steps:
        name='final' if final else ('angel' if side==0 else 'demon')
        with wave.open(str(RES/f'forge_hammer_{name}.wav'),'rb') as f:
            clip=np.frombuffer(f.readframes(f.getnframes()),dtype='<i2')/32768
        onset=round((start+duration*.54)*44100)
        track[onset:onset+len(clip)]+=clip*(.95 if final else .72)
    assert np.max(np.abs(track)) < 1
    with tempfile.TemporaryDirectory(prefix='forge-preview-') as tmp:
        tmp=Path(tmp)
        wav=tmp/'contact.wav'
        with wave.open(str(wav),'wb') as f:
            f.setparams((1,2,44100,len(track),'NONE','not compressed'))
            f.writeframes(np.rint(track*32767).astype('<i2').tobytes())
        for frame in range(math.ceil(total*fps)):
            now=frame/fps; a=d=0; active=0; final=False; spark=0; reveal=False
            for start,duration,side,last in steps:
                if now < start: break
                active=side; final=last
                elapsed=now-start
                pose=1 if elapsed < duration*.46 else (0 if elapsed < duration*.54 else 2)
                if elapsed >= duration and not last: pose=0
                if elapsed>=duration*.54 and elapsed<duration:
                    spark=1-(elapsed-duration*.54)/(duration*.46)
                else: spark=0
                a=pose if side==0 else 0; d=pose if side==1 else 0
                reveal=last and elapsed>=duration
            scene.render(a,d,active,final,not angel_final,spark,True,reveal).save(tmp/f'{frame:04}.png')
        subprocess.run(['ffmpeg','-v','error','-y','-framerate',str(fps),'-i',str(tmp/'%04d.png'),
            '-i',str(wav),'-c:v','libx264','-pix_fmt','yuv420p','-crf','24','-c:a','aac','-b:a','128k',
            '-shortest','-movflags','+faststart',str(path)],check=True)
    print(f'{path.name}: 7 nominal synchronized contact events, total {total:.2f}s; NOT a Unity recording')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--video',action='store_true')
    args=parser.parse_args(); args.output.mkdir(parents=True,exist_ok=True)
    for height,top,bottom in ((1920,0,0),(2340,90,60),(2400,100,70),(2520,110,80)):
        scene=Scene(height,top,bottom)
        print(f'1080x{height}: actor {900*scene.scale:.1f}px; stage top {scene.stage_top:.1f}; result {scene.result_top:.1f}, height {scene.plate_height:.1f}; layout bounds pass')
        scene.render(0,2,1,True,True,0,True,True).save(args.output/f'forge-{height}-demon-double-design.jpg',quality=92)
    Scene(2340,90,60).render(2,0,0,True,False,0,True,True).save(args.output/'forge-angel-slip-design.jpg',quality=92)
    if args.video:
        make_video(args.output/'forge-demon-double-design.mp4',False)
        make_video(args.output/'forge-angel-slip-design.mp4',True)
