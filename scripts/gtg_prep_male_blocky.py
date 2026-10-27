# ============================================================================
# NOTICE: Full documentation, design decisions, and fix history for this file
# live in docs/GrandTheftGrimoire/blender.md, section "gtg_prep_male_blocky.py"
# ============================================================================
"""
Prepares the Male_Blocky character of Gtg_Character_Base_Template.blend for Unity
and exports it as one FBX. Written for Blender 3.3 and newer.

Run from Blender: open the .blend, switch to the Scripting workspace, open this
file in the Text Editor, press Run Script. The FBX and a report are written next
to the .blend.

Run headless:
    blender -b Gtg_Character_Base_Template.blend --python gtg_prep_male_blocky.py -- --out /path/Gtg_Male_Blocky.fbx

Options after the double dash: --out FILE, --keep (leave the temporary objects in
the scene), --no-verify (skip the re-import check).

The scene is not changed: all work happens on temporary copies that are removed
at the end, and the .blend is never saved by this script.
"""

import os
import sys
import math

import bpy
from mathutils import Matrix, Vector

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

SOURCE_COLLECTION = 'Male_Blocky'
SOURCE_ARMATURE = 'Armature'
EXPORT_NAME = 'Gtg_Male_Blocky'
RIG_NAME = 'GTG_Rig'
BODY_NAME = 'Body'
TEMP_COLLECTION = 'GTG_Prep_Temp'

# One Blender unit in this file is about 0.2 m (the body is about 9 units tall).
SCALE = 0.2

# Height of the Spine to Chest weight blend on the torso, in source units.
TORSO_BLEND = 0.7

MATERIAL_SKIN = 'Gtg_Skin'
MATERIAL_FACE = 'Gtg_Face'

# (object name, rule, bone or None, material)
# Bicep holds the upper arm block and Tricep the forearm block (checked by position).
PARTS = [
    ('Face', 'fixed', 'Head', MATERIAL_FACE),
    ('Ear', 'fixed', 'Head', MATERIAL_SKIN),
    ('Neck', 'fixed', 'Neck', MATERIAL_SKIN),
    ('Torso', 'torso', None, MATERIAL_SKIN),
    ('Pelvis_N', 'fixed', 'Hip', MATERIAL_SKIN),
    ('Theigh', 'side', 'Leg_Upper.{s}', MATERIAL_SKIN),
    ('Leg.002', 'side', 'Leg_Lower.{s}', MATERIAL_SKIN),
    ('Feet', 'side', 'Foot.{s}', MATERIAL_SKIN),
    ('Bicep', 'side', 'Arm_Upper.{s}', MATERIAL_SKIN),
    ('Tricep', 'side', 'Arm_Lower.{s}', MATERIAL_SKIN),
    ('Hand', 'hand', None, MATERIAL_SKIN),
]

SIDES = ('L', 'R')

# Two name typos in the rig, fixed once here so every later part uses the right names.
BONE_RENAMES = []
for _s in SIDES:
    BONE_RENAMES.append(('RingFibger_1.' + _s, 'RingFinger_1.' + _s))
    BONE_RENAMES.append(('ThumbFinger_1.' + _s + '.001', 'ThumbFinger_3.' + _s))

# Foot and Wrist hang under IK bones in the source rig. They move under the lower leg and lower arm.
REPARENT = [('Foot.{s}', 'Leg_Lower.{s}'), ('Wrist.{s}', 'Arm_Lower.{s}')]

# Blender-only helper bones. Unity rebuilds IK with the Animation Rigging package.
HELPER_BONES = []
for _s in SIDES:
    HELPER_BONES += ['Leg_IK.' + _s, 'Leg_Target.' + _s, 'Wrist_IK.' + _s, 'Arm_IK.' + _s]

FINGERS = ('Thumb', 'Index', 'Middle', 'Ring', 'Pinky')

FBX_OPTIONS = dict(
    use_selection=True,
    object_types={'ARMATURE', 'MESH'},
    use_mesh_modifiers=False,
    use_armature_deform_only=True,
    add_leaf_bones=False,
    bake_anim=False,
    axis_forward='-Z',
    axis_up='Y',
    global_scale=1.0,
    apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_UNITS',
    bake_space_transform=False,
    mesh_smooth_type='OFF',
    primary_bone_axis='Y',
    secondary_bone_axis='X',
    path_mode='AUTO',
    embed_textures=False,
    use_custom_props=False,
)


class PrepError(Exception):
    pass


LOG = []


def log(line=''):
    LOG.append(line)
    print(line)


# ---------------------------------------------------------------------------
# Geometry helpers
# ---------------------------------------------------------------------------

def dist_to_segment(p, a, b):
    ab = b - a
    denom = ab.length_squared
    t = 0.0 if denom == 0.0 else max(0.0, min(1.0, (p - a).dot(ab) / denom))
    return (p - (a + ab * t)).length


def side_of(x, what):
    if abs(x) < 0.05:
        raise PrepError('%s sits on the centre line, so its side cannot be told' % what)
    return 'L' if x > 0 else 'R'


# ---------------------------------------------------------------------------
# Step 1: read the source parts into one list of world-space geometry
# ---------------------------------------------------------------------------

def read_parts(tmp):
    coll = bpy.data.collections.get(SOURCE_COLLECTION)
    if coll is None:
        raise PrepError('collection %s not found' % SOURCE_COLLECTION)
    by_name = {o.name: o for o in coll.objects if o.type == 'MESH'}
    wanted = [p[0] for p in PARTS]
    missing = [n for n in wanted if n not in by_name]
    if missing:
        raise PrepError('mesh objects missing from %s: %s' % (SOURCE_COLLECTION, ', '.join(missing)))
    skipped = sorted(n for n in by_name if n not in wanted)
    if skipped:
        log('Skipped meshes not in the part table: ' + ', '.join(skipped))

    materials = []
    verts, faces, uvs, face_mat, vert_part, islands = [], [], [], [], [], []
    copies = []
    for name, rule, bone, mat_name in PARTS:
        src = by_name[name]
        # A copy is linked and shown so its modifiers are evaluated even if the source is hidden.
        cp = src.copy()
        cp.hide_viewport = False
        cp.hide_render = False
        tmp.objects.link(cp)
        cp.hide_set(False)
        copies.append(cp)
    dg = bpy.context.evaluated_depsgraph_get()
    dg.update()

    for (name, rule, bone, mat_name), cp in zip(PARTS, copies):
        ev = cp.evaluated_get(dg)
        mw = ev.matrix_world.copy()
        flip = mw.determinant() < 0
        me = bpy.data.meshes.new_from_object(ev, preserve_all_data_layers=True, depsgraph=dg)
        try:
            mods = [m.type for m in cp.modifiers]
            off = [m.name for m in cp.modifiers if not m.show_viewport]
            if off:
                log('WARNING %s: modifiers off in the viewport and not applied: %s' % (name, ', '.join(off)))
            if any(t != 'MIRROR' for t in mods):
                log('WARNING %s: applied modifiers %s (blocky parts normally have only Mirror)' % (name, mods))
            if mat_name not in materials:
                materials.append(mat_name)
            mi = materials.index(mat_name)
            uv_layer = me.uv_layers.active
            base = len(verts)
            for v in me.vertices:
                verts.append(mw @ v.co)
                vert_part.append(name)
            parent = list(range(len(me.vertices)))

            def find(i):
                while parent[i] != i:
                    parent[i] = parent[parent[i]]
                    i = parent[i]
                return i

            for poly in me.polygons:
                idx = list(poly.vertices)
                loops = list(range(poly.loop_start, poly.loop_start + poly.loop_total))
                if flip:
                    idx.reverse()
                    loops.reverse()
                for k in range(1, len(idx)):
                    ra, rb = find(idx[0]), find(idx[k])
                    if ra != rb:
                        parent[rb] = ra
                faces.append(tuple(base + i for i in idx))
                face_mat.append(mi)
                for li in loops:
                    if uv_layer is not None:
                        uvs.append(tuple(uv_layer.data[li].uv))
                    else:
                        uvs.append((0.0, 0.0))
            groups = {}
            for i in range(len(me.vertices)):
                groups.setdefault(find(i), []).append(base + i)
            for members in groups.values():
                islands.append({'part': name, 'verts': members})
        finally:
            bpy.data.meshes.remove(me)
    return {
        'verts': verts, 'faces': faces, 'uvs': uvs, 'face_mat': face_mat,
        'vert_part': vert_part, 'islands': islands, 'materials': materials, 'copies': copies,
    }


# ---------------------------------------------------------------------------
# Step 2: the rig copy
# ---------------------------------------------------------------------------

def ensure_object_mode():
    ob = bpy.context.object
    if ob is not None and ob.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')


def make_rig_copy(tmp):
    src = bpy.data.objects.get(SOURCE_ARMATURE)
    if src is None or src.type != 'ARMATURE':
        raise PrepError('armature object %s not found' % SOURCE_ARMATURE)
    mw = src.matrix_world.copy()
    rot_scale = mw.to_3x3()
    for r in range(3):
        for c in range(3):
            if abs(rot_scale[r][c] - (1.0 if r == c else 0.0)) > 1e-4:
                raise PrepError('armature object %s has rotation or scale; apply it first' % SOURCE_ARMATURE)
    offset = mw.translation.copy()
    data = src.data.copy()
    data.name = RIG_NAME
    obj = bpy.data.objects.new(RIG_NAME, data)
    tmp.objects.link(obj)
    return obj, offset


def edit_bones_session(arm_obj, fn):
    ensure_object_mode()
    bpy.context.view_layer.objects.active = arm_obj
    arm_obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    try:
        fn(arm_obj.data.edit_bones)
    finally:
        bpy.ops.object.mode_set(mode='OBJECT')


def fix_rig(arm_obj, offset, notes):
    def work(eb):
        # Object transform into the bones, so the object can be reset to identity.
        targets = {b.name: (b.head + offset, b.tail + offset) for b in eb}
        for name, (h, t) in targets.items():
            eb[name].head = h
            eb[name].tail = t
        for old, new in BONE_RENAMES:
            if old not in eb:
                raise PrepError('bone %s not found' % old)
            eb[old].name = new
            notes.append('renamed %s -> %s' % (old, new))
        for child_pat, parent_pat in REPARENT:
            for s in SIDES:
                c, p = child_pat.format(s=s), parent_pat.format(s=s)
                if c not in eb or p not in eb:
                    raise PrepError('bone %s or %s not found' % (c, p))
                old_parent = eb[c].parent.name if eb[c].parent else None
                eb[c].parent = eb[p]
                eb[c].use_connect = False
                notes.append('parent of %s: %s -> %s' % (c, old_parent, p))
        # Hips and Spine start at the same point in the source. Hips moves to the
        # middle between the leg roots and the Spine start.
        for need in ('Hip', 'Spine', 'Leg_Upper.L', 'Leg_Upper.R'):
            if need not in eb:
                raise PrepError('bone %s not found' % need)
        hip, spine = eb['Hip'], eb['Spine']
        legs_mid = (eb['Leg_Upper.L'].head + eb['Leg_Upper.R'].head) * 0.5
        new_head = (legs_mid + spine.head) * 0.5
        old_head = hip.head.copy()
        hip.head = new_head
        hip.tail = spine.head.copy()
        notes.append('Hip head moved (%.3f, %.3f, %.3f) -> (%.3f, %.3f, %.3f)' % (
            old_head.x, old_head.y, old_head.z, new_head.x, new_head.y, new_head.z))
        for name in HELPER_BONES:
            if name not in eb:
                continue
            kids = [b.name for b in eb[name].children]
            if kids:
                raise PrepError('helper bone %s still has children: %s' % (name, ', '.join(kids)))
            eb.remove(eb[name])
        notes.append('removed helper bones: ' + ', '.join(HELPER_BONES))

    edit_bones_session(arm_obj, work)
    arm_obj.matrix_world = Matrix.Identity(4)


def bone_segments(arm_obj):
    return {b.name: (b.head_local.copy(), b.tail_local.copy()) for b in arm_obj.data.bones}


# ---------------------------------------------------------------------------
# Step 3: weights
# ---------------------------------------------------------------------------

def need_bone(segs, name):
    if name not in segs:
        raise PrepError('bone %s not found in the rig' % name)
    return segs[name]


def compute_weights(geo, segs, report):
    verts = geo['verts']
    weights = [None] * len(verts)
    warnings = []

    def centroid(members):
        c = Vector((0.0, 0.0, 0.0))
        for i in members:
            c += verts[i]
        return c / len(members)

    deform_names = [n for n in segs if n not in ('Root',)]

    def check_nearest(part, bone, c):
        a, b = need_bone(segs, bone)
        d = dist_to_segment(c, a, b)
        best = min(deform_names, key=lambda n: dist_to_segment(c, *segs[n]))
        if best != bone:
            warnings.append('%s: centroid is nearest to %s, assigned %s (%.2f away)' % (part, best, bone, d))

    for isl in geo['islands']:
        part = isl['part']
        rule = [p for p in PARTS if p[0] == part][0]
        members = isl['verts']
        c = centroid(members)
        if rule[1] == 'fixed':
            bone = rule[2]
            need_bone(segs, bone)
            check_nearest(part, bone, c)
            for i in members:
                weights[i] = [(bone, 1.0)]
        elif rule[1] == 'side':
            bone = rule[2].format(s=side_of(c.x, part + ' island'))
            need_bone(segs, bone)
            check_nearest(part, bone, c)
            for i in members:
                weights[i] = [(bone, 1.0)]
        elif rule[1] == 'torso':
            sp_a, sp_b = need_bone(segs, 'Spine')
            ch_a, ch_b = need_bone(segs, 'Chest')
            joint = ch_a.z
            half = TORSO_BLEND * 0.5
            for i in members:
                t = (verts[i].z - (joint - half)) / (2.0 * half)
                t = max(0.0, min(1.0, t))
                if t <= 0.0:
                    weights[i] = [('Spine', 1.0)]
                elif t >= 1.0:
                    weights[i] = [('Chest', 1.0)]
                else:
                    weights[i] = [('Spine', 1.0 - t), ('Chest', t)]
        elif rule[1] == 'hand':
            side = side_of(c.x, 'Hand island')
            chains = {'Wrist': ['Wrist.' + side]}
            for f in FINGERS:
                chains[f] = ['%sFinger_%d.%s' % (f, k, side) for k in (1, 2, 3)]
            for names in chains.values():
                for n in names:
                    need_bone(segs, n)
            best_chain = min(chains, key=lambda k: min(dist_to_segment(c, *segs[n]) for n in chains[k]))
            names = chains[best_chain]
            for i in members:
                bone = min(names, key=lambda n: dist_to_segment(verts[i], *segs[n]))
                weights[i] = [(bone, 1.0)]
        else:
            raise PrepError('unknown rule %s' % rule[1])

    bad = [i for i, w in enumerate(weights) if not w or abs(sum(x[1] for x in w) - 1.0) > 1e-6]
    if bad:
        raise PrepError('%d vertices have no weight or weights that do not sum to 1' % len(bad))
    for w in warnings:
        report.append('WARNING ' + w)
    return weights


# ---------------------------------------------------------------------------
# Step 4: final transform, body object, export
# ---------------------------------------------------------------------------

def bake_scale_into_rig(arm_obj, shift_z):
    def work(eb):
        targets = {}
        for b in eb:
            h = (b.head + Vector((0.0, 0.0, shift_z))) * SCALE
            t = (b.tail + Vector((0.0, 0.0, shift_z))) * SCALE
            targets[b.name] = (h, t)
        # Root goes exactly to the ground.
        if 'Root' in targets:
            h, t = targets['Root']
            d = -h.z
            targets['Root'] = (h + Vector((0.0, 0.0, d)), t + Vector((0.0, 0.0, d)))
        for name, (h, t) in targets.items():
            eb[name].head = h
            eb[name].tail = t

    edit_bones_session(arm_obj, work)


def make_materials(names, created):
    mats = []
    for n in names:
        m = bpy.data.materials.get(n)
        if m is None:
            m = bpy.data.materials.new(n)
            created.append(m)
        mats.append(m)
    return mats


def build_body(geo, weights, arm_obj, tmp, created_mats):
    shift_z = -min(v.z for v in geo['verts'])
    verts = [((v + Vector((0.0, 0.0, shift_z))) * SCALE)[:] for v in geo['verts']]
    me = bpy.data.meshes.new(BODY_NAME)
    me.from_pydata(verts, [], geo['faces'])
    uv = me.uv_layers.new(name='UVMap')
    flat = []
    for u, v in geo['uvs']:
        flat.extend((u, v))
    uv.data.foreach_set('uv', flat)
    for m in make_materials(geo['materials'], created_mats):
        me.materials.append(m)
    me.polygons.foreach_set('material_index', geo['face_mat'])
    me.update()
    body = bpy.data.objects.new(BODY_NAME, me)
    tmp.objects.link(body)
    groups = {}
    for i, w in enumerate(weights):
        for bone, val in w:
            groups.setdefault(bone, []).append((i, val))
    for bone in sorted(groups):
        vg = body.vertex_groups.new(name=bone)
        for i, val in groups[bone]:
            vg.add([i], val, 'REPLACE')
    mod = body.modifiers.new('Armature', 'ARMATURE')
    mod.object = arm_obj
    body.parent = arm_obj
    return body, shift_z, groups


def run_export(path, arm_obj, body):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    arm_obj.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = arm_obj
    props = bpy.ops.export_scene.fbx.get_rna_type().properties.keys()
    kwargs = {k: v for k, v in FBX_OPTIONS.items() if k in props}
    ignored = sorted(k for k in FBX_OPTIONS if k not in props)
    if ignored:
        log('FBX options this Blender does not have, left out: ' + ', '.join(ignored))
    bpy.ops.export_scene.fbx(filepath=path, **kwargs)


# ---------------------------------------------------------------------------
# Step 5: re-import check
# ---------------------------------------------------------------------------

def verify_export(path, expected_bones, expected_verts, results):
    before_obj = set(bpy.data.objects.keys())
    before_mesh = set(bpy.data.meshes.keys())
    before_arm = set(bpy.data.armatures.keys())
    before_mat = set(bpy.data.materials.keys())
    before_act = set(bpy.data.actions.keys())
    bpy.ops.import_scene.fbx(filepath=path)
    new_objs = [bpy.data.objects[n] for n in bpy.data.objects.keys() if n not in before_obj]
    try:
        arms = [o for o in new_objs if o.type == 'ARMATURE']
        meshes = [o for o in new_objs if o.type == 'MESH']
        results.append(('one armature and one mesh in the file', len(arms) == 1 and len(meshes) == 1,
                        '%d armatures, %d meshes' % (len(arms), len(meshes))))
        if arms:
            names = set(b.name for b in arms[0].data.bones)
            exp = set(expected_bones)
            results.append(('bone names match the rig', names == exp,
                            'missing %s, extra %s' % (sorted(exp - names), sorted(names - exp)) if names != exp else '%d bones' % len(names)))
            helpers = sorted(n for n in names if n.split('.')[0] in ('Leg_IK', 'Leg_Target', 'Wrist_IK', 'Arm_IK'))
            results.append(('no IK helper bones', not helpers, ', '.join(helpers) or 'none'))
        if meshes:
            me = meshes[0]
            results.append(('vertex count kept', len(me.data.vertices) == expected_verts,
                            '%d of %d' % (len(me.data.vertices), expected_verts)))
            results.append(('vertex groups present', len(me.vertex_groups) > 0, '%d groups' % len(me.vertex_groups)))
            results.append(('armature modifier present', any(m.type == 'ARMATURE' for m in me.modifiers), ''))
            pts = [me.matrix_world @ v.co for v in me.data.vertices]
            zs = [p.z for p in pts]
            results.append(('mesh stands on z = 0 after import', abs(min(zs)) < 1e-3, 'lowest point %.5f' % min(zs)))
            results.append(('mesh is about 1.8 high after import', abs((max(zs) - min(zs)) - 1.8) < 0.05,
                            'height %.4f' % (max(zs) - min(zs))))
            ys = [p.y for p in pts]
            results.append(('mesh depth axis is Y after import', True, 'y range %.3f to %.3f' % (min(ys), max(ys))))
    finally:
        for o in new_objs:
            bpy.data.objects.remove(o, do_unlink=True)
        for n in list(bpy.data.meshes.keys()):
            if n not in before_mesh:
                bpy.data.meshes.remove(bpy.data.meshes[n])
        for n in list(bpy.data.armatures.keys()):
            if n not in before_arm:
                bpy.data.armatures.remove(bpy.data.armatures[n])
        for n in list(bpy.data.materials.keys()):
            if n not in before_mat:
                bpy.data.materials.remove(bpy.data.materials[n])
        for n in list(bpy.data.actions.keys()):
            if n not in before_act:
                bpy.data.actions.remove(bpy.data.actions[n])


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def purge_temp():
    # Leftovers from an earlier run that used --keep.
    old = bpy.data.collections.get(TEMP_COLLECTION)
    if old is not None:
        for o in list(old.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.collections.remove(old)
    for a in list(bpy.data.armatures):
        if a.users == 0 and a.name.startswith(RIG_NAME):
            bpy.data.armatures.remove(a)
    for m in list(bpy.data.meshes):
        if m.users == 0 and m.name.startswith(BODY_NAME):
            bpy.data.meshes.remove(m)
    for m in list(bpy.data.materials):
        if m.users == 0 and m.name in (MATERIAL_SKIN, MATERIAL_FACE):
            bpy.data.materials.remove(m)


def remember_selection():
    vl = bpy.context.view_layer
    active = vl.objects.active.name if vl.objects.active else None
    selected = [o.name for o in vl.objects if o.select_get()]
    return active, selected


def restore_selection(state):
    active, selected = state
    vl = bpy.context.view_layer
    for o in vl.objects:
        o.select_set(False)
    for n in selected:
        o = bpy.data.objects.get(n)
        if o is not None and o.name in vl.objects:
            o.select_set(True)
    ao = bpy.data.objects.get(active) if active else None
    vl.objects.active = ao if (ao is not None and ao.name in vl.objects) else None


def uv_note(geo):
    # Every part is unwrapped into the full 0-1 square, so parts overlap in UV space.
    spans = {}
    k = 0
    for f in geo['faces']:
        part = geo['vert_part'][f[0]]
        for _ in f:
            u, v = geo['uvs'][k]
            k += 1
            r = spans.setdefault(part, [9.0, 9.0, -9.0, -9.0])
            r[0] = min(r[0], u)
            r[1] = min(r[1], v)
            r[2] = max(r[2], u)
            r[3] = max(r[3], v)
    wide = sorted(p for p, r in spans.items() if (r[2] - r[0]) * (r[3] - r[1]) > 0.3)
    if len(wide) > 1:
        log('NOTE UVs are copied as they are. %d parts each cover a large part of the 0-1 square, '
            'so a texture would repeat on all of them. Pack the UVs before texturing.' % len(wide))


def parse_args():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    opts = {'out': None, 'keep': False, 'verify': True}
    i = 0
    while i < len(argv):
        if argv[i] == '--out' and i + 1 < len(argv):
            opts['out'] = argv[i + 1]
            i += 1
        elif argv[i] == '--keep':
            opts['keep'] = True
        elif argv[i] == '--no-verify':
            opts['verify'] = False
        i += 1
    return opts


def default_out_path():
    base = bpy.path.abspath('//') if bpy.data.filepath else os.path.expanduser('~')
    return os.path.join(base, EXPORT_NAME + '.fbx')


def write_report(fbx_path):
    txt_path = os.path.splitext(fbx_path)[0] + '_prep_report.txt'
    text = '\n'.join(LOG) + '\n'
    try:
        with open(txt_path, 'w') as f:
            f.write(text)
    except OSError:
        txt_path = None
    block = bpy.data.texts.get('GTG_prep_report')
    if block is None:
        block = bpy.data.texts.new('GTG_prep_report')
    block.clear()
    block.write(text)
    return txt_path


def main():
    opts = parse_args()
    out_path = os.path.abspath(opts['out'] or default_out_path())
    LOG.clear()
    log('GTG prep, Blender %s' % bpy.app.version_string)
    log('Source: %s, armature %s' % (SOURCE_COLLECTION, SOURCE_ARMATURE))
    results = []
    notes = []
    picked = remember_selection()
    purge_temp()
    tmp = bpy.data.collections.new(TEMP_COLLECTION)
    bpy.context.scene.collection.children.link(tmp)
    created_mats = []
    status = 'FAILED'
    body = arm_obj = None
    geo = None
    try:
        ensure_object_mode()
        geo = read_parts(tmp)
        log('Read %d parts: %d vertices, %d faces, %d islands' % (
            len(PARTS), len(geo['verts']), len(geo['faces']), len(geo['islands'])))
        uv_note(geo)
        arm_obj, offset = make_rig_copy(tmp)
        fix_rig(arm_obj, offset, notes)
        for n in notes:
            log('Rig: ' + n)
        segs = bone_segments(arm_obj)
        weights = compute_weights(geo, segs, LOG)
        body, shift_z, groups = build_body(geo, weights, arm_obj, tmp, created_mats)
        bake_scale_into_rig(arm_obj, shift_z)
        zs = [v.co.z for v in body.data.vertices]
        xs = [v.co.x for v in body.data.vertices]
        ys = [v.co.y for v in body.data.vertices]
        log('Body: %d vertices, height %.4f m, lowest point %.5f, width %.3f m, depth %.3f m' % (
            len(zs), max(zs) - min(zs), min(zs), max(xs) - min(xs), max(ys) - min(ys)))
        log('Scale %.3f and a shift of %.4f source units so the soles sit at z = 0' % (SCALE, shift_z))
        log('')
        log('Vertices per bone (heaviest weight):')
        primary = {}
        for w in weights:
            top = max(w, key=lambda x: x[1])[0]
            primary[top] = primary.get(top, 0) + 1
        for b in sorted(primary):
            log('  %-18s %d' % (b, primary[b]))
        unweighted = sorted(n for n in segs if n not in primary and n != 'Root')
        log('Bones with no vertices (kept in the rig): ' + ', '.join(unweighted))
        ls = sum(v for k, v in primary.items() if k.endswith('.L'))
        rs = sum(v for k, v in primary.items() if k.endswith('.R'))
        if ls != rs:
            log('WARNING left bones carry %d vertices and right bones %d' % (ls, rs))
        os.makedirs(os.path.dirname(out_path), exist_ok=True)
        run_export(out_path, arm_obj, body)
        log('')
        log('Wrote ' + out_path)
        if opts['verify']:
            try:
                verify_export(out_path, [b.name for b in arm_obj.data.bones], len(body.data.vertices), results)
            except Exception as exc:
                results.append(('re-import check ran', False, str(exc)))
            log('Re-import check:')
            for label, ok, detail in results:
                log('  %-4s %s %s' % ('ok' if ok else 'FAIL', label, ('(' + detail + ')') if detail else ''))
        status = 'OK' if all(r[1] for r in results) else 'CHECK THE FAIL LINES'
    except PrepError as exc:
        log('STOPPED: ' + str(exc))
    finally:
        if not opts['keep']:
            for o in list(tmp.objects):
                bpy.data.objects.remove(o, do_unlink=True)
            bpy.data.collections.remove(tmp)
            for m in list(bpy.data.meshes):
                if m.users == 0:
                    bpy.data.meshes.remove(m)
            for a in list(bpy.data.armatures):
                if a.users == 0 and a.name.startswith(RIG_NAME):
                    bpy.data.armatures.remove(a)
            for m in created_mats:
                if m.users == 0:
                    bpy.data.materials.remove(m)
        restore_selection(picked)
    log('RESULT: ' + status)
    report = write_report(out_path)
    if report:
        print('Report written to ' + report)
    return status


if __name__ == '__main__':
    main()
