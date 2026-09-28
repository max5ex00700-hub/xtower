#!/usr/bin/env python3
"""Static/key-pose design preview, NOT a Unity or device recording.

Requires Pillow, numpy, PyMuPDF, ffmpeg. Uses the actual PNGs and 11.62 geometry.
"""
import argparse
import io
import math
from pathlib import Path
import subprocess
import tempfile

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
        self.result_top = height-self.bottom-80-220
        self.scale = max(.4, min(996/1080, (self.result_top-self.top-330)/950))
        self.center_y = self.result_top-400*self.scale-26
        self.contact = (0, 31)  # 3x2 cells, 84px high, base at -53
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
                frames.append(frame.resize((round(300*self.scale),)*2,Image.Resampling.LANCZOS))
            self.poses.append(frames)
        anvil=Image.open(RES/'forge_anvil.bytes').convert('RGBA')
        self.anvil=anvil.resize((round(260*self.scale),round(520/3*self.scale)),Image.Resampling.LANCZOS)
        # Contact is computed from the same measured image coordinates as C#.
        self.homes=[]
        for x,y in ((.923,.746),(.102,.787)):
            self.homes.append((self.contact[0]-(x-.5)*600,self.contact[1]-(.5-y)*600))
        assert self.center_y+370*self.scale < self.result_top
        assert self.top+298 < self.center_y-520*self.scale

    def point(self,x,y):
        return ((540+x*self.scale)/2,(self.center_y-y*self.scale)/2)

    def paste(self,image,sprite,x,y):
        px,py=self.point(x,y)
        image.alpha_composite(sprite,(round(px-sprite.width/2),round(py-sprite.height/2)))

    def render(self,angel_pose=0,demon_pose=0,active=0,final=False,success=True,spark=0):
        image=self.background.copy()
        label(image,'대장간 · 분해',540,self.top+50,68,'#FFE0A3')
        label(image,'성공률 60%',540,self.top+159,56,'#FFD46E')
        label(image,'제물 가치 6  ·  활',540,self.top+253,30,'#DCE3F5')
        order=(1,0) if active==0 else (0,1)
        for side in order:
            pose=(angel_pose,demon_pose)[side]
            self.paste(image,self.poses[side][pose],*self.homes[side])
        self.paste(image,self.anvil,0,-190)
        draw=ImageDraw.Draw(image)
        for y in range(2):
            for x in range(3):
                cx=(x+.5)*42-63
                cy=-53+84-(y+.5)*42
                px,py=self.point(cx,cy)
                half=20*self.scale/2
                draw.rectangle((px-half,py-half,px+half,py+half),fill='#FFBD45')
                draw.rectangle((px-half*.7,py-half*.7,px+half*.7,py+half*.7),fill='#140E09')
        if spark:
            px,py=self.point(*self.contact)
            for i in range(20):
                angle=i*math.pi*2/20
                length=(45 if i%3==0 else 25)*self.scale*spark
                draw.line((px,py,px+math.cos(angle)*length,py+math.sin(angle)*length),
                    fill='#FFDD61' if active==0 else '#FF5328',width=2)
        y=self.result_top/2
        draw.rectangle((21,y,519,y+110),fill='#070C15')
        draw.line((61,y,479,y),fill='#BC873F',width=1)
        text=('천사의 마지막 일격' if success else '악마의 마지막 일격') if final else ('천사의 타격' if active==0 else '악마의 타격')
        label(image,text,540,self.result_top+53,32,'#A8E3FF' if active==0 else '#FF8266')
        if final:
            label(image,'분해 성공' if success else '분해 실패',540,self.result_top+142,70,'#FFE380' if success else '#FF573F')
        label(image,'천사 막타: 성공  ·  악마 막타: 실패',540,self.height-self.bottom-38,28,'#F0DBB3')
        return image.convert('RGB')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args(); args.output.mkdir(parents=True,exist_ok=True)
    for height,top,bottom in ((1920,0,0),(2340,90,60),(2400,100,70),(2520,110,80)):
        scene=Scene(height,top,bottom)
        print(f'1080x{height}: safe insets {top}/{bottom}; geometry and 6 frame-alpha checks passed')
        if height in (1920,2400):
            scene.render(2,0,0,True,True,.6).save(args.output/f'forge-{height}-success-design.jpg',quality=93)
    scene=Scene(1920)
    scene.render(0,2,1,True,False,.6).save(args.output/'forge-failure-design.jpg',quality=93)
    timeline=[(0,0,0,False,0)]*6
    for i in range(6):
        active=i%2
        for pose,ticks,spark in ((1,3,0),(0,1,0),(2,2,.7),(0,1,0)):
            timeline.extend([(pose if active==0 else 0,pose if active==1 else 0,active,False,spark)]*ticks)
    timeline.extend([(1,0,0,False,0)]*8)
    timeline.extend([(2,0,0,True,1)]*3)
    timeline.extend([(2,0,0,True,0)]*15)
    with tempfile.TemporaryDirectory(prefix='forge-preview-') as temporary:
        for i,(a,d,active,final,spark) in enumerate(timeline):
            scene.render(a,d,active,final,True,spark).save(Path(temporary)/f'{i:04}.png')
        subprocess.run(['ffmpeg','-v','error','-y','-framerate','12','-i',temporary+'/%04d.png',
            '-c:v','libx264','-pix_fmt','yuv420p','-crf','23','-movflags','+faststart',
            str(args.output/'forge-angel-finisher-design.mp4')],check=True)
    print('Saved a pose-sequence design preview. This is NOT a Unity recording.')
