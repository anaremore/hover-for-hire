"""Build the original HFH-6 civilian utility helicopter. Run with Blender --background --python.

All dimensions below are in Unity metres: X right, Y up, Z forward. No downloaded
assets, fonts, or textures are required. The exported mesh has no physics geometry.
"""
import bpy
import math
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/HoverForHire/Resources/Art/Helicopter'
OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

def xyz(p):
    return (p[0], -p[2], p[1])

def material(name, color, metallic=0, rough=.4):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    bs = m.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value = (*color, 1)
    bs.inputs['Metallic'].default_value = metallic
    bs.inputs['Roughness'].default_value = rough
    return m

M = {
    'orange': material('HFH_OrangeEnamel', (.95,.265,.045), .28, .29),
    'cream': material('HFH_CreamEnamel', (.91,.88,.73), .2, .35),
    'charcoal': material('HFH_Graphite', (.032,.046,.057), .42, .31),
    'steel': material('HFH_BrushedAlloy', (.36,.41,.43), .82, .28),
    'darksteel': material('HFH_DarkAlloy', (.092,.11,.12), .72, .36),
    'rubber': material('HFH_Rubber', (.015,.022,.023), .02, .83),
    'seat': material('HFH_CabinFabric', (.10,.14,.145), .02, .92),
    'glass': material('HFH_CanopyGlass', (.08,.24,.29), .05, .08),
    'light': material('HFH_LandingLight', (.85,.94,1), .15, .2),
    'red': material('HFH_NavigationRed', (1,.023,.01), .1, .22),
    'green': material('HFH_NavigationGreen', (.01,.85,.29), .1, .22),
    'display': material('HFH_DisplayCyan', (.17,.79,.78), .05, .42),
    'yellow': material('HFH_CautionYellow', (1,.69,.06), .15, .36),
}
glassbs=M['glass'].node_tree.nodes.get('Principled BSDF')
glassbs.inputs['Transmission Weight'].default_value=.85
glassbs.inputs['IOR'].default_value=1.43
groups = {'body': [], 'glass': [], 'rotor': [], 'tailrotor': [], 'light': [], 'tail': [], 'skids': [], 'door_left': [], 'door_right': []}
gauge_names=['airspeed','altitude','vertical speed','rotor rpm','heading']
for gauge in gauge_names:groups['needle '+gauge]=[]

def finish(obj, name, mat, group='body', bevel=0):
    obj.name=name
    obj.data.materials.append(M[mat])
    if bevel:
        mod=obj.modifiers.new('Manufactured edge radius','BEVEL');mod.width=bevel;mod.segments=2
        bpy.context.view_layer.objects.active=obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    for poly in obj.data.polygons: poly.use_smooth=True
    if bevel:
        mod=obj.modifiers.new('Weighted surface normals','WEIGHTED_NORMAL');mod.keep_sharp=True;mod.weight=50
        bpy.context.view_layer.objects.active=obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    if group=='body':
        if name.startswith(('Tapered tail','Tail structural','Swept vertical','Fin orange','Horizontal stabilizer','Stabilizer tip','Tail bumper','Tail gearbox')):group='tail'
        elif name.startswith(('Landing skid','Bent skid','Skid rubber','Skid saddle','Replaceable skid')):group='skids'
    groups[group].append(obj)
    return obj

def mesh(name, verts, faces, mat, group='body', bevel=0):
    data=bpy.data.meshes.new(name);data.from_pydata([xyz(v) for v in verts],[],faces);data.update()
    obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj)
    return finish(obj,name,mat,group,bevel)

def cube(name, p, size, mat, bevel=.015, group='body'):
    bpy.ops.mesh.primitive_cube_add(size=1,location=xyz(p));obj=bpy.context.object
    obj.scale=(size[0],size[2],size[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(obj,name,mat,group,bevel)

def ellipsoid(name, p, size, mat, group='body', segments=20):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=10,location=xyz(p));obj=bpy.context.object
    obj.scale=(size[0]/2,size[2]/2,size[1]/2);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(obj,name,mat,group)

def tube(name, points, radius, mat, group='body', resolution=8):
    curve=bpy.data.curves.new(name,'CURVE');curve.dimensions='3D';curve.resolution_u=1
    curve.bevel_depth=radius;curve.bevel_resolution=2;curve.resolution_u=resolution
    spline=curve.splines.new('POLY');spline.points.add(len(points)-1)
    for point,p in zip(spline.points,points):point.co=(*xyz(p),1)
    obj=bpy.data.objects.new(name,curve);bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active=obj;obj.select_set(True);bpy.ops.object.convert(target='MESH');obj.select_set(False)
    return finish(obj,name,mat,group)

def cylinder(name, a,b,r,mat,group='body',vertices=16,r2=None):
    av=Vector(xyz(a));bv=Vector(xyz(b));d=bv-av
    bpy.ops.mesh.primitive_cone_add(vertices=vertices,radius1=r,radius2=r if r2 is None else r2,depth=d.length,location=(av+bv)/2)
    obj=bpy.context.object;obj.rotation_euler=d.to_track_quat('Z','Y').to_euler()
    return finish(obj,name,mat,group)

def line_interpolate(a,b,count=10):
    return [tuple(a[i]+(b[i]-a[i])*j/(count-1) for i in range(3)) for j in range(count)]

# Rounded but deliberately engineered cross sections, with a tapered lower nose
# and a broad cabin. The glazed shell has real openings in its opaque mesh.
sections=[(-1.75,.18,-.39,.45),(-1.5,.66,-.67,.78),(-1.14,1.00,-.79,1.02),
          (-.5,1.09,-.82,1.07),(.30,1.055,-.77,1.065),(1.04,.96,-.61,.99),
          (1.60,.76,-.39,.72),(2.00,.38,-.20,.36),(2.14,.04,-.04,.12)]
angles=[-180,-165,-150,-135,-120,-105,-90,-75,-60,-45,-30,-15,0,15,30,45,60,75,90,105,120,135,150,165]
def surface(section,angle,inflate=0):
    z,width,bottom,top=section;a=math.radians(angle)
    c=math.cos(a);s=math.sin(a)
    y=(top+bottom)/2+(top-bottom)/2*math.copysign(abs(c)**.82,c)
    x=width*math.copysign(abs(s)**.90,s)
    return (x+inflate*s,y+inflate*c,z)

def shell_patch(si,ai,mat,group='body'):
    aj=(ai+1)%len(angles)
    if group=='body' and si in (2,3) and 105<abs(angles[ai]+7.5)<150:
        group='door_left' if angles[ai]<0 else 'door_right'
    return mesh('Cabin surface', [surface(sections[si],angles[ai]),surface(sections[si],angles[aj]),
        surface(sections[si+1],angles[aj]),surface(sections[si+1],angles[ai])],[(0,1,2,3)],mat,group)

for si in range(len(sections)-1):
    for ai,angle in enumerate(angles):
        mid=angle+7.5
        side_window=si in (2,3,4) and 45<=abs(mid)<=97.5
        windshield=si in (5,6) and abs(mid)<=82.5
        if side_window or windshield: shell_patch(si,ai,'glass','glass')
        else:
            mat='orange' if abs(mid)<112.5 else 'charcoal'
            if 97.5<=abs(mid)<127.5:mat='cream'
            shell_patch(si,ai,mat)
mesh('Forward nose closure',[surface(sections[-1],a) for a in angles],[tuple(reversed(range(len(angles))))],'orange')
mesh('Aft pressure bulkhead',[surface(sections[0],a) for a in angles],[tuple(range(len(angles)))],'charcoal')
for si in (2,3,4):
    for a in (-45,-30,-15,0,15,30):
        mesh('Cabin headliner',[surface(sections[si],a,-.018),surface(sections[si],a+15,-.018),
            surface(sections[si+1],a+15,-.018),surface(sections[si+1],a,-.018)],[(3,2,1,0)],'seat')
# Window perimeter strips and door pillars follow the actual shell, not a cube.
for side in (-1,1):
    for a in (45,105):
        tube('Window perimeter seal',[surface(sections[s],side*a,.012) for s in range(2,6)],.028,'rubber')
        tube('Window bright trim',[surface(sections[s],side*a,.022) for s in range(2,6)],.012,'cream')
    for si in (2,4,5):
        tube('Cabin window pillar',[surface(sections[si],side*a,.016) for a in (45,60,75,90,105)],.044,'orange')
        tube('Cabin window seal',[surface(sections[si],side*a,.025) for a in (47,60,75,90,103)],.022,'rubber')
        tube('Cabin inner pillar',[surface(sections[si],side*a,-.021) for a in (45,60,75,90,105)],.034,'rubber')
    # Thin lower door seams, exposed hinges, latch, step tread, rivet rows.
    door=[surface(sections[2],side*a,.018) for a in (105,120,135)]
    door += [surface(sections[s],side*135,.018) for s in (3,4)]
    door += [surface(sections[4],side*a,.018) for a in (120,105)]
    tube('Aft cabin door seam',door,.009,'darksteel')
    for z in (-1.05,-.48,.18):
        cylinder('Door hinge',(side*1.063,-.17,z-.052),(side*1.063,-.17,z+.052),.028,'steel')
    tube('Recessed door handle',[(side*1.082,-.10,-.04),(side*1.13,-.09,-.04),(side*1.13,-.09,.18),(side*1.074,-.10,.18)],.018,'steel')
    for si in (2,3,4):
        for a in (113,126,139,152):
            p=surface(sections[si],side*a,.027)
            ellipsoid('Flush airframe fastener',p,(.021,.021,.021),'steel',segments=8)
    for z in (-.60,-.4,-.2,0,.2,.4):
        cube('Side foot step',(side*1.065,-.93,z),(.28,.035,.027),'darksteel',.007)
    tube('Step rail',[(side*.91,-.79,-.72),(side*1.20,-.94,-.61),(side*1.20,-.94,.54),(side*.94,-.79,.62)],.034,'steel')

for si in (5,7):
    tube('Windshield surround',[surface(sections[si],a,.015) for a in range(-90,91,15)],.035,'rubber')
    tube('Windshield painted surround',[surface(sections[si],a,.027) for a in range(-90,91,15)],.014,'cream')
tube('Windshield center spine',[surface(sections[si],0,.015) for si in (5,6,7)],.024,'charcoal')
for side in (-1,1):
    tube('Windshield outside frame',[surface(sections[si],side*90,.015) for si in (5,6,7)],.032,'cream')
    tube('Windshield inner frame',[surface(sections[si],side*90,-.016) for si in (5,6,7)],.026,'rubber')
    # Windscreen wiper pivot, arm and blade, kept low in the field of view.
    tube('Windshield wiper',[(side*.10,.46,1.86),(side*.22,.58,1.73),(side*.54,.61,1.57)],.012,'darksteel')
    tube('Wiper blade',[(side*.45,.59,1.63),(side*.67,.58,1.49)],.018,'rubber')

# Composite upper engine cowling with separate intake lips, louvers and exhaust.
def loft(name,rings,mat,count=24):
    verts=[]
    for z,rx,cy,ry in rings:
        for j in range(count):
            a=j/count*math.tau;verts.append((math.sin(a)*rx,cy+math.cos(a)*ry,z))
    faces=[]
    for k in range(len(rings)-1):
        for j in range(count):faces.append((k*count+j,k*count+(j+1)%count,(k+1)*count+(j+1)%count,(k+1)*count+j))
    faces.extend([tuple(reversed(range(count))),tuple((len(rings)-1)*count+j for j in range(count))])
    return mesh(name,verts,faces,mat)
loft('Turboshaft engine cowling',[(-1.57,.24,.78,.15),(-1.35,.51,1.02,.33),(-.95,.59,1.09,.34),(-.30,.52,1.17,.31),(.24,.25,1.20,.19)],'cream')
for side in (-1,1):
    ellipsoid('Engine inlet shadow',(side*.435,1.20,-.24),(.27,.29,.40),'rubber')
    tube('Engine inlet rim',[(side*.44,1.065,-.20),(side*.56,1.16,-.13),(side*.52,1.31,-.14),(side*.36,1.35,-.20)],.023,'steel')
    for z in (-1.24,-1.15,-1.06,-.97,-.88):
        cube('Cowling cooling louver',(side*.55,1.09,z),(.016,.205,.036),'charcoal',.007)
    cylinder('Exhaust outer pipe',(side*.35,1.04,-1.24),(side*.43,1.02,-1.74),.14,'darksteel',r2=.12)
    cylinder('Exhaust hollow',(side*.432,1.021,-1.74),(side*.435,1.021,-1.763),.095,'rubber')
    cylinder('Exhaust rolled lip',(side*.428,1.022,-1.72),(side*.436,1.020,-1.766),.126,'steel')
    cylinder('Exhaust aperture',(side*.437,1.020,-1.766),(side*.439,1.020,-1.775),.105,'rubber')

# Tapered structural tail boom, panel bands and a swept fin.
loft('Tapered tail boom',[(-1.46,.33,.18,.35),(-1.85,.27,.23,.28),(-2.8,.195,.38,.205),(-4.15,.115,.58,.145),(-5.02,.072,.70,.12)],'orange',20)
for z,r,y in [(-2.0,.264,.26),(-3.0,.193,.40),(-4.15,.122,.58)]:
    tube('Tail structural seam',[(math.sin(a*math.tau/20)*r,y+math.cos(a*math.tau/20)*r,z) for a in range(21)],.008,'darksteel')

def extruded_profile(name,profile,width,mat):
    verts=[(side*width/2,y,z) for side in (-1,1) for y,z in profile];n=len(profile)
    faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(j,(j+1)%n,(j+1)%n+n,j+n) for j in range(n)]
    return mesh(name,verts,faces,mat,bevel=.012)
extruded_profile('Swept vertical stabilizer',[(.35,-4.85),(.46,-5.35),(2.05,-5.29),(2.11,-4.99),(.94,-4.69)],.12,'cream')
extruded_profile('Fin orange identifier',[(1.54,-5.30),(2.05,-5.29),(2.10,-4.99),(1.61,-4.85)],.137,'orange')
for side in (-1,1):
    mesh('Horizontal stabilizer',[(side*.06,.55,-3.93),(side*1.13,.58,-4.22),(side*1.10,.59,-4.73),(side*.055,.58,-4.48),
        (side*.06,.62,-3.93),(side*1.13,.65,-4.22),(side*1.10,.66,-4.73),(side*.055,.65,-4.48)],
        [(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],'orange',bevel=.009)
    cube('Stabilizer tip',(side*1.10,.61,-4.49),(.085,.1,.43),'cream',.016)
tube('Tail bumper',[(0,.30,-4.75),(0,.08,-5.04),(0,.12,-5.42),(0,.42,-5.50)],.032,'steel')

# Curved tubular skids follow the existing contact boxes, including kicked-up toes.
for side in (-1,1):
    tube('Landing skid',[(side,-1.27,-1.76),(side,-1.36,-1.6),(side,-1.39,-1.3),(side,-1.39,1.45),
        (side,-1.375,1.66),(side,-1.31,1.84),(side,-1.15,1.99),(side,-1.08,2.03)],.082,'darksteel')
    for z in (-.88,.94):
        tube('Bent skid cross tube',[(0,-.73,z),(side*.42,-.77,z),(side*.68,-.84,z),(side*.82,-1.02,z),(side,-1.32,z)],.061,'steel')
        cylinder('Skid rubber shock sleeve',(side*.765,-.952,z),(side*.889,-1.15,z),.081,'rubber')
        cube('Skid saddle',(side,-1.32,z),(.18,.14,.23),'charcoal',.025)
        cylinder('Skid saddle fastener',(side-.11,-1.30,z),(side+.11,-1.30,z),.024,'steel')
    for z in (-1.30,-.20,1.17):cube('Replaceable skid wear plate',(side,-1.465,z),(.19,.032,.29),'steel',.011)

# Cabin is open internally: floor, rear bulkhead, two seats, harnesses, dashboard,
# analogue dials, buttons, radio face, cyclics, collectives and anti-torque pedals.
mesh('Cabin floor',[(-.77,-.60,-1.05),(.77,-.60,-1.05),(.78,-.60,.76),(.46,-.41,1.39),(-.46,-.41,1.39),(-.78,-.60,.76)],[(0,1,2,3,4,5)],'charcoal')
cube('Rear bulkhead',(0,.06,-1.12),(1.62,1.23,.065),'seat',.035)
for side in (-1,1):
    seatx=side*.43
    cube('Seat pedestal',(seatx,-.42,.22),(.43,.36,.53),'darksteel',.035)
    cube('Seat cushion',(seatx,-.205,.31),(.69,.145,.66),'seat',.065)
    back=cube('Seat sculpted back',(seatx,.21,-.035),(.69,.78,.17),'seat',.065)
    back.rotation_euler[0]=math.radians(-9)
    cube('Head restraint',(seatx,.70,-.115),(.39,.25,.13),'rubber',.044)
    for dx in (-.17,.17):
        tube('Seat shoulder harness',[(seatx+dx,.59,.04),(seatx+dx*.8,.10,.083),(seatx+dx*.6,-.095,.28)],.025,'rubber')
    cube('Harness buckle',(seatx,-.084,.31),(.1,.04,.075),'steel',.01)
    tube('Cyclic control',[(seatx,-.58,.76),(seatx,-.20,.75),(seatx,-.08,.62)],.027,'steel')
    cylinder('Cyclic grip',(seatx,-.07,.62),(seatx,.06,.60),.046,'rubber')
    ellipsoid('Cyclic trim hat',(seatx,.068,.615),(.035,.018,.028),'red',segments=8)
    tube('Collective lever',[(seatx+side*.30,-.30,.03),(seatx+side*.31,-.18,.36),(seatx+side*.31,-.10,.46)],.029,'darksteel')
    cylinder('Collective rubber grip',(seatx+side*.31,-.16,.34),(seatx+side*.31,-.095,.47),.045,'rubber')
    for dx in (-.15,.15):
        cylinder('Pedal link',(seatx+dx,-.59,1.19),(seatx+dx,-.46,1.29),.018,'steel')
        cube('Anti torque pedal',(seatx+dx,-.45,1.3),(.19,.065,.1),'rubber',.009)

# Slim console leaves the forward and downward approach view unobstructed.
cube('Instrument console base',(0,-.40,1.12),(.43,.42,.39),'charcoal',.035)
dash=cube('Instrument panel',(0,.10,1.32),(1.29,.42,.22),'charcoal',.055)
cube('Instrument brow',(0,.34,1.33),(1.42,.055,.34),'rubber',.024)
def gauge_label(label,p,size=.014):
    data=bpy.data.curves.new('Gauge engraved label','FONT');data.body=label;data.size=size;data.align_x='CENTER';data.align_y='CENTER';data.resolution_u=2
    obj=bpy.data.objects.new(label,data);bpy.context.collection.objects.link(obj);obj.location=xyz(p);obj.rotation_euler=(math.pi/2,0,math.pi)
    bpy.context.view_layer.objects.active=obj;obj.select_set(True);bpy.ops.object.convert(target='MESH');obj.select_set(False)
    finish(obj,'Instrument legend / '+label,'cream')
gauge_labels=[('KTS','0   140'),('ALT m','0   1000'),('V/S','-10   +10'),('RPM %','0   100'),('HDG','N')]
for i,x in enumerate((-.47,-.24,0,.24,.47)):
    y=.12 if i%2==0 else .13
    cylinder('Dial chrome bezel',(x,y,1.192),(x,y,1.176),.085,'steel',vertices=24)
    cylinder('Dial black face',(x,y,1.175),(x,y,1.169),.073,'rubber',vertices=24)
    for j in range(8 if i==4 else 9):
        a=math.radians(j*45 if i==4 else -130+j*260/8)
        tube('Instrument tick',[(x+math.sin(a)*.052,y+math.cos(a)*.052,1.164),
              (x+math.sin(a)*.064,y+math.cos(a)*.064,1.164)],.0024,'cream')
    tube('Gauge needle '+gauge_names[i],[(x,y,1.160),(x,y+.050,1.160)],.004,'display','needle '+gauge_names[i])
    ellipsoid('Dial spindle',(x,y,1.158),(.015,.015,.009),'cream',segments=8)
    gauge_label(gauge_labels[i][0],(x,y-.024,1.162),.014)
    gauge_label(gauge_labels[i][1],(x,y-.045,1.162),.010)
cube('Radio equipment face',(0,-.12,1.194),(.34,.14,.028),'darksteel',.012)
cube('Radio illuminated readout',(0,-.095,1.174),(.21,.028,.007),'display',.003)
for side in (-1,1):cylinder('Radio knob',(side*.133,-.12,1.174),(side*.133,-.12,1.15),.02,'rubber')
for i in range(7):
    x=-.49+i*.16
    cube('Warning annunciator',(x,.278,1.20),(.061,.025,.01),'green' if i in (1,2,3,5) else 'yellow',.004)
    cylinder('Console switch',(x,-.038,1.194),(x,-.025,1.164),.007,'steel',vertices=8)

# Small monochrome navigation/status displays complement the analogue cluster.
for side in (-1,1):
    x=side*.735
    cube('Navigation display housing',(x,.11,1.25),(.21,.32,.15),'charcoal',.023)
    cube('Navigation display screen',(x,.13,1.170),(.17,.22,.008),'rubber',.005)
    for d in (-.07,0,.07):
        tube('MFD horizontal grid',[(x-.075,.13+d,1.163),(x+.075,.13+d,1.163)],.0018,'display')
        tube('MFD vertical grid',[(x+d,.03,1.163),(x+d,.23,1.163)],.0018,'display')
    tube('MFD aircraft caret',[(x-.021,.098,1.158),(x,.142,1.158),(x+.021,.098,1.158),(x,.108,1.158),(x-.021,.098,1.158)],.003,'display')
    for d in (-.066,-.033,0,.033,.066):cube('MFD function key',(x+d,-.027,1.164),(.021,.022,.018),'steel',.004)

# Mast, stationary swashplate, pitch links and articulated four-blade rotor.
cylinder('Transmission mast',(0,1.25,-.07),(0,1.98,-.07),.093,'steel')
cylinder('Mast lower boot',(0,1.31,-.07),(0,1.53,-.07),.13,'rubber')
cylinder('Swashplate',(0,1.59,-.07),(0,1.65,-.07),.255,'steel')
cylinder('Rotor head',(0,1.98,-.07),(0,2.09,-.07),.24,'darksteel','rotor')
for i in range(4):
    angle=i*math.pi/2
    def rp(x,y,z): return (x*math.cos(angle)-z*math.sin(angle),y,x*math.sin(angle)+z*math.cos(angle)-.07)
    cylinder('Pitch control rod',rp(.17,1.64,.08),rp(.33,2.02,.08),.022,'steel')
    cylinder('Blade grip',rp(.1,2.055,0),rp(.64,2.055,0),.062,'steel','rotor')
    cylinder('Flapping hinge',rp(.34,2.04,-.1),rp(.34,2.04,.1),.038,'darksteel','rotor')
    profile=[(.53,2.052,-.09),(.86,2.056,-.19),(4.33,2.015,-.12),(4.62,1.989,-.025),
             (4.59,1.973,.12),(.85,2.020,.13),(.53,2.021,.09)]
    n=len(profile);verts=[rp(x,y+dy,z) for dy in (0,.038) for x,y,z in profile]
    faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(j,(j+1)%n,(j+1)%n+n,j+n) for j in range(n)]
    mesh('Composite main rotor blade',verts,faces,'charcoal','rotor',.005)
    mesh('Rotor high visibility tip',[rp(x,y,z) for x,y,z in [(4.28,2.058,-.12),(4.62,2.030,-.025),(4.59,2.016,.12),(4.28,2.055,.12)]],[(0,1,2,3)],'cream','rotor')
cylinder('Rotor hub cap',(0,2.09,-.07),(0,2.16,-.07),.145,'cream','rotor')

# Tail rotor turns about X in Unity, attached to a stationary gearbox.
tp=(.235,.90,-5.09)
cylinder('Tail gearbox',(-.055,.90,-5.09),(.24,.90,-5.09),.17,'darksteel')
cylinder('Tail rotor hub',(.25,.90,-5.09),(.38,.90,-5.09),.105,'steel','tailrotor')
for i in range(3):
    a=i*math.tau/3
    def tr(u,v,x=.34):return (x,.90+u*math.cos(a)-v*math.sin(a),-5.09+u*math.sin(a)+v*math.cos(a))
    profile=[(.08,-.038),(.20,-.080),(.80,-.071),(.87,.0),(.84,.09),(.20,.066)]
    verts=[tr(u,v,x) for x in (.32,.353) for u,v in profile];n=len(profile)
    mesh('Tail rotor blade',verts,[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(j,(j+1)%n,(j+1)%n+n,j+n) for j in range(n)],'charcoal','tailrotor',.004)
    mesh('Tail rotor yellow tip',[tr(u,v,.356) for u,v in [(.70,-.073),(.80,-.071),(.87,0),(.84,.09),(.70,.084)]],[(0,1,2,3,4)],'yellow','tailrotor')

# Antennae, avionics blister, nose landing light and nav lenses.
tube('VHF aerial',[(0,1.15,-1.0),(0,1.75,-1.45)],.014,'charcoal')
tube('Belly antenna',[(0,-.76,-.58),(0,-1.08,-.93)],.012,'charcoal')
ellipsoid('GPS puck',(0,1.13,.54),(.29,.10,.26),'cream')
cylinder('Landing lamp housing',(0,-.30,1.81),(0,-.31,1.91),.115,'charcoal')
cylinder('Landing lamp reflector',(0,-.31,1.91),(0,-.31,1.921),.09,'steel')
cylinder('Landing lamp glass',(0,-.31,1.922),(0,-.31,1.928),.07,'light','light')
for side,mat in ((1,'red'),(-1,'green')):
    ellipsoid('Navigation fairing',(side*1.032,.02,-.67),(.19,.15,.30),'charcoal')
    ellipsoid('Navigation lens',(side*1.105,.047,-.65),(.07,.074,.12),mat,'light')
ellipsoid('Anti collision beacon',(0,1.48,-.90),(.105,.14,.105),'red','light')
ellipsoid('Tail position light',(0,2.135,-5.125),(.075,.066,.095),'light','light')

# Original taxi livery and registration. Blender's built-in vector font is baked
# to geometry, so no system font or texture is required at runtime.
def text_label(label,p,size,mat,side=1):
    data=bpy.data.curves.new('Airframe lettering','FONT');data.body=label;data.size=size;data.align_x='CENTER';data.align_y='CENTER'
    data.extrude=0;data.resolution_u=2
    obj=bpy.data.objects.new(label,data);bpy.context.collection.objects.link(obj);obj.location=xyz(p)
    # Local text X runs towards nose on left, towards tail on right, local Y is up.
    from mathutils import Matrix
    right=Vector((0,side,0));up=Vector((0,0,1));normal=right.cross(up)
    obj.rotation_euler=Matrix((right,up,normal)).transposed().to_euler()
    bpy.context.view_layer.objects.active=obj;obj.select_set(True);bpy.ops.object.convert(target='MESH');obj.select_set(False)
    # Conform the cabin paint to the actual curved airframe instead of floating
    # a flat text plane in space. Tail registrations stay on the flatter boom.
    if label in ('HOVER','AIR TAXI'):
        transform=obj.matrix_world.copy();inverse=transform.inverted()
        for vertex in obj.data.vertices:
            world=transform@vertex.co;y=world.z;z=-world.y
            for lower,upper in zip(sections,sections[1:]):
                if lower[0]<=z<=upper[0]:
                    t=(z-lower[0])/(upper[0]-lower[0]);width=lower[1]+t*(upper[1]-lower[1]);bottom=lower[2]+t*(upper[2]-lower[2]);top=lower[3]+t*(upper[3]-lower[3])
                    c=max(-1,min(1,(y-(top+bottom)/2)/((top-bottom)/2)));c=math.copysign(abs(c)**(1/.82),c)
                    world.x=side*(width*(max(0,1-c*c)**.5)**.90+.012)
                    vertex.co=inverse@world
                    break
    finish(obj,'Paint / '+label,mat)
for side in (-1,1):
    text_label('HOVER',(side*1.031,-.22,-.37),.167,'charcoal',side)
    text_label('AIR TAXI',(side*.99,-.49,-.34),.086,'cream',side)
    text_label('HFH-06',(side*.142,.63,-4.3),.113,'charcoal',side)
    # Checker taxi stripe, made from tiny paint-thickness tiles.
    for col in range(12):
        z=-1.05+col*.087
        for row in range(2):
            sec=sections[2] if z<-.5 else sections[3];nxt=sections[3] if z<-.5 else sections[4]
            t=(z-sec[0])/(nxt[0]-sec[0]);width=sec[1]+t*(nxt[1]-sec[1])
            cube('Taxi checker stripe',(side*(width*.993+.007),-.032+row*.042,z),(.009,.041,.086),'charcoal' if (row+col)%2==0 else 'cream',0)

# Batch static geometry and use a handful of renderers / material submeshes.
def join_group(group,name,origin=(0,0,0)):
    bpy.ops.object.select_all(action='DESELECT')
    for o in groups[group]:o.select_set(True)
    bpy.context.view_layer.objects.active=groups[group][0];bpy.ops.object.join();obj=bpy.context.object;obj.name=name
    bpy.context.scene.cursor.location=xyz(origin);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    # Recalculate outward surface normals after joining generated mesh patches.
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.remove_doubles(threshold=.00005);bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
    return obj
body=join_group('body','Airframe and cabin')
canopy=join_group('glass','Glazed canopy')
main=join_group('rotor','Main rotor (visual only)',(0,2.055,-.07))
tail=join_group('tailrotor','Tail rotor (visual only)',tp)
lights=join_group('light','Exterior lenses')
tail_assembly=join_group('tail','Tail assembly',(0,.30,-1.60))
skids=join_group('skids','Landing skids',(0,-1.1,0))
left_door=join_group('door_left','Cabin door left',(-1,-.3,-.4))
right_door=join_group('door_right','Cabin door right',(1,-.3,-.4))
exported=[body,canopy,main,tail,lights,tail_assembly,skids,left_door,right_door]
for i,x in enumerate((-.47,-.24,0,.24,.47)):
    y=.12 if i%2==0 else .13
    exported.append(join_group('needle '+gauge_names[i],'Gauge needle '+gauge_names[i],(x,y,1.160)))

# Correct normals on glass must face out; runtime disables culling on the canopy
# so the pilot sees the coating from inside without an opaque duplicate shell.
bpy.ops.object.select_all(action='DESELECT')
for o in exported:o.select_set(True)
bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
bpy.ops.export_scene.fbx(filepath=str(OUT/'HFH_Utility_Helicopter.fbx'),use_selection=True,object_types={'MESH'},
    apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',
    bake_space_transform=True,add_leaf_bones=False,mesh_smooth_type='FACE',use_mesh_modifiers=True,path_mode='STRIP')
triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in exported)
(OUT/'asset-notes.txt').write_text(f'Original HFH-6 utility helicopter. Generated with Tools/Art/build_helicopter.py.\n'
    f'{triangles:,} triangles; fourteen mesh renderers including five live instrument needles; no colliders, external images or fonts.\n'
    'Metres; Unity +Z forward / +Y up. Pilot eye (0.43, 0.63, 0.10), 5 degrees down.\n'
    'URP materials configured by HelicopterVisual.cs. Source models are generated offline.\n')
print(f'EXPORTED HFH helicopter: {triangles:,} triangles -> {OUT}')

# Offline art preview, not included as a game dependency.
preview=ROOT/'Artifacts/Art';preview.mkdir(parents=True,exist_ok=True)
ground=material('Preview concrete',(.15,.17,.17),.05,.8)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-1.5));bpy.context.object.data.materials.append(ground)
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=32
scene.world.color=(.30,.35,.40)
for name,power,loc,size in [('Key',1600,(4,-2,8),7),('Fill',900,(-5,-4,4),6),('Rim',2200,(0,7,6),5)]:
    bpy.ops.object.light_add(type='AREA',location=loc);bpy.context.object.name=name;bpy.context.object.data.energy=power;bpy.context.object.data.shape='DISK';bpy.context.object.data.size=size
    direction=Vector((0,1,0))-bpy.context.object.location;bpy.context.object.rotation_euler=direction.to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(8,-10,5.0));cam=bpy.context.object
direction=Vector((0,1,0))-cam.location;cam.rotation_euler=direction.to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=11.8;scene.camera=cam
scene.view_settings.view_transform='AgX';scene.render.resolution_x=1600;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(preview/'helicopter-studio.png')
bpy.ops.wm.save_as_mainfile(filepath=str(preview/'helicopter-source.blend'))
bpy.ops.render.render(write_still=True)
canopy.hide_render=True  # Geometry/sightline preview; runtime uses alpha glazing.
cam.location=xyz((-.43,.63,.10));cam.data.type='PERSP';cam.data.lens=17
direction=Vector(xyz((-.43,.37,3.0)))-cam.location;cam.rotation_euler=direction.to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(preview/'helicopter-cockpit.png')
bpy.ops.render.render(write_still=True)
