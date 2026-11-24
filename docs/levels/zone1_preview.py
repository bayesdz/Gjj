import numpy as np
from PIL import Image, ImageDraw, ImageFont
rng=np.random.default_rng(3)
# value-noise stand-in for Mathf.PerlinNoise
G=256; tbl=rng.random((G,G))
def perlin(x,y):
    x=np.asarray(x)%G; y=np.asarray(y)%G
    x0=np.floor(x).astype(int); y0=np.floor(y).astype(int); fx=x-x0; fy=y-y0
    sx=fx*fx*(3-2*fx); sy=fy*fy*(3-2*fy)
    x1=(x0+1)%G; y1=(y0+1)%G
    a=tbl[x0,y0]; b=tbl[x1,y0]; c=tbl[x0,y1]; d=tbl[x1,y1]
    return (a*(1-sx)+b*sx)*(1-sy)+(c*(1-sx)+d*sx)*sy
def N(x,z,f,o): return perlin(x*f+o, z*f+o*1.37+17.3)
rooms=[(0,0,11,0),(8,48,20,-1),(-18,100,22,-2),(-54,110,8,-1.5),(4,125,10,-2.5),(30,155,26,-3),(58,200,7,-2)]
cor=[(0,0,-5,22,5),(-5,22,8,48,5.5),(8,48,-8,76,5),(-8,76,-18,100,5.5),(-18,100,-54,110,4),(-18,100,4,125,5),(4,125,30,155,5),(30,155,50,186,4.5),(50,186,58,200,4.5)]
CA=(-11,138.2); CB=(19,111.8)
def seg(x,z,a,b):
    ax,az=a;bx,bz=b;dx,dz=bx-ax,bz-az
    t=np.clip(((x-ax)*dx+(z-az)*dz)/(dx*dx+dz*dz),0,1)
    return np.hypot(x-(ax+dx*t), z-(az+dz*t))
def sdf(x,z):
    d=np.full_like(x,1e9)
    for rx,rz,r,h in rooms: d=np.minimum(d,np.hypot(x-rx,z-rz)-r)
    for ax,az,bx,bz,r in cor: d=np.minimum(d,seg(x,z,(ax,az),(bx,bz))-r)
    return d+(N(x,z,.07,100)-.5)*4+(N(x,z,.25,300)-.5)*1.2
def chasm(x,z): return seg(x,z,CA,CB)-5+(N(x,z,.18,1500)-.5)*2
def floor(x,z):
    sw=0;sh=0
    for rx,rz,r,h in rooms:
        d=np.maximum(0,np.hypot(x-rx,z-rz)-r); w=1/(d*d+16); sw=sw+w; sh=sh+w*h
    return sh/sw+(N(x,z,.05,500)-.5)*1.2
def ss(a,b,x):
    t=np.clip((x-a)/(b-a),0,1); return t*t*(3-2*t)
def height(x,z):
    h=floor(x,z)+(N(x,z,.4,700)-.5)*.35; d=sdf(x,z)
    n=N(x,z,.12,900)
    cliff=ss(0,4,d)*(5+4*n)+ss(2,20,d)*(16+14*n)+(N(x,z,.35,1100)-.5)*3*ss(0,3,d)
    h=np.where(d>0,h+cliff,h)
    dc=chasm(x,z); bottom=-34+(N(x,z,.3,1700)-.5)*6
    return np.where(dc<0, h+(bottom-h)*ss(0,3,-dc), h)
S=4  # px per m
xs=np.arange(-90,110,1/S*1.0); zs=np.arange(-40,240,1/S)
X,Z=np.meshgrid(xs,zs[::-1])
H=height(X,Z); D=sdf(X,Z)
gy,gx=np.gradient(H, 1/S)
nx=-gx; nz=gy; ny=np.ones_like(H)
L=np.array([-0.5,1.2,0.6]); L/=np.linalg.norm(L)
shade=np.clip((nx*L[0]+ny*L[1]+nz*L[2])/np.sqrt(nx**2+ny**2+nz**2),0,1)
fl=floor(X,Z); rel=H-fl
col=np.zeros(H.shape+(3,))
base_floor=np.array([.30,.28,.34]); wall=np.array([.24,.23,.29]); high=np.array([.13,.12,.18]); ch=np.array([.35,.06,.05])
col[:]=wall
col[rel>11]=high
col[(rel<1.5)]=base_floor
col[rel<-3]=ch
img=col*(0.25+0.9*shade[...,None])
# darken by height above floor (fog from top)
img*=np.clip(1-np.maximum(rel,0)/45,0.25,1)[...,None]
img=(np.clip(img,0,1)*255).astype(np.uint8)
im=Image.fromarray(img).convert('RGB'); dr=ImageDraw.Draw(im)
def P(x,z): return ((x+90)*S, (240-z)*S)
try: f=ImageFont.truetype('/usr/share/fonts/truetype/inter/Inter-SemiBold.ttf',22)
except: 
    import glob; ff=glob.glob('/usr/share/fonts/**/Inter*.ttf',recursive=True)+glob.glob('/usr/share/fonts/**/*.ttf',recursive=True); f=ImageFont.truetype(ff[0],22)
def dot(x,z,c,r=7,label=None):
    px,py=P(x,z); dr.ellipse([px-r,py-r,px+r,py+r],fill=c,outline=(10,10,10))
    if label: dr.text((px+r+4,py-12),label,fill=(240,240,240),font=f)
# path
pts=[(0,-4),(-5,22),(8,48),(-8,76),(-18,100),(4,125),(30,155),(50,186),(58,200)]
dr.line([P(*p) for p in pts],fill=(255,255,255),width=2)
# bridge
import math
u=np.array([48,55.0]);u/=np.linalg.norm(u)
a=(4-u[0]*10,125-u[1]*10); b=(4+u[0]*10,125+u[1]*10)
dr.line([P(*a),P(*b)],fill=(170,160,150),width=15)
dot(0,-4,(124,252,154),8,'Старт')
for (x,z),n in [((11,56),'Г1'),((-20,104),'Г2'),((24,150),'Г3')]: dot(x,z,(255,77,109),8,n)
dot(40,156,(255,240,180),10,'Клетка Сияющего')
dot(-26,94,(255,140,40),8,'Костёр')
dot(-16,120,(20,20,20),9,'Нора')
dot(-13,116,(245,197,66),7,'Игрушка')
dot(-58,112,(245,197,66),7,'Рисунок')
dot(60.5,204,(79,209,197),10,'К вратам эльфов')
for x,z in [(22,44),(-6,52),(16,64),(20,34),(-34,90),(-4,94),(44,144),(16,168),(50,168),(10,150)]: dot(x,z,(90,85,110),9)
for x,z in [(-30,88),(-34,112),(-26,116),(-36,98),(-22,84)]: dot(x,z,(160,110,70),8)
im.save('/tmp/claude-0/-home-user-Gjj/cb25da61-148d-56df-8944-6270aa240f08/scratchpad/preview/top.png')
# connectivity check: walkable = D<0 & chasm>0, plus bridge
walk=(D<0)&(chasm(X,Z)>0)
# add bridge strip
bx=(X-4)*u[0]+(Z-125)*u[1]; by=-(X-4)*u[1]+(Z-125)*u[0]
walk|=(np.abs(bx)<10)&(np.abs(by)<1.9)
from collections import deque
def idx(x,z): return (int(round((240-z)*S)), int(round((x+90)*S)))
start=idx(0,-4); seen=np.zeros_like(walk); q=deque([start]); seen[start]=True
while q:
    i,j=q.popleft()
    for di,dj in ((1,0),(-1,0),(0,1),(0,-1)):
        a2,b2=i+di,j+dj
        if 0<=a2<walk.shape[0] and 0<=b2<walk.shape[1] and walk[a2,b2] and not seen[a2,b2]:
            seen[a2,b2]=True; q.append((a2,b2))
for name,(x,z) in {'g1':(11,56),'g2':(-20,104),'g3':(24,150),'cage':(33,157),'toy':(-13,116),'draw':(-55,111),'exit':(58,200),'fire_side':(-22,94)}.items():
    i,j=idx(x,z); print(name, bool(seen[i,j]), 'walk' if walk[i,j] else 'NOT walk', 'h=%.1f'%H[i,j])
print('min corridor check ok')
walk2=(D<0)&(chasm(X,Z)>0)
seen=np.zeros_like(walk2); q=deque([start]); seen[start]=True
while q:
    i,j=q.popleft()
    for di,dj in ((1,0),(-1,0),(0,1),(0,-1)):
        a2,b2=i+di,j+dj
        if 0<=a2<walk2.shape[0] and 0<=b2<walk2.shape[1] and walk2[a2,b2] and not seen[a2,b2]:
            seen[a2,b2]=True; q.append((a2,b2))
i,j=idx(30,155); print('cage hall reachable WITHOUT bridge:', bool(seen[i,j]))
