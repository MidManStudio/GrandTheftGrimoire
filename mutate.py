import subprocess, shutil, os, sys
SRC='/home/claude/work/GrandTheftGrimoire/Assets/MidManStudio/Gtg/Managed/Building'
RIG='/home/claude/work/GrandTheftGrimoire/Assets/MidManStudio/Gtg/Managed/Camera/ManagedCameraRig.cs'
PROJ='/home/claude/work/harness/building-test'
muts=[
 ('ManagedBuildGrid.cs','- width * 0.5f + 0.5f','- width * 0.5f + 0f','anchor rounding'),
 ('ManagedBuildGrid.cs','| (uint)z','| z','key packing'),
 ('ManagedPlacedStore.cs','_bucketIndex[moved] = _bucketIndex[pieceId];','','swap remove index fix-up'),
 ('ManagedPlacedStore.cs','removed(gone[i]);','','Clear events'),
 ('ManagedPlacementValidator.cs','if (high - low > rules.MaxGroundDelta)','if (false)','uneven check'),
 ('ManagedPlacementValidator.cs','> rules.MaxReach * rules.MaxReach','< rules.MaxReach * rules.MaxReach','reach inverted'),
 ('ManagedPlacementValidator.cs','float boxHeight = def.HeightMeters - rules.BlockerSkin;','float boxHeight = def.HeightMeters;','blocker skin'),
 ('ManagedPlacementController.cs','if (!Pay(def))','if (false)','payment skipped'),
 ('ManagedPlacementController.cs','Result = new ManagedPlacementResult { Status = ManagedPlacementStatus.NoAim };\n        }\n\n    ','Result = new ManagedPlacementResult();\n        }\n\n    ','ctor result'),
 ('ManagedPlacementController.cs','RestoreCarried();\n            }\n\n            Leave();','\n            }\n\n            Leave();','cancel does not restore'),
 ('ManagedPlacementController.cs','_stock.Give(Definition.Id, 1);\n                Leave();','Leave();','stash does not give'),
 ('ManagedPlacementController.cs','&& !CommitOnRelease\n                && CheckAvailability','&& CheckAvailability','drag keeps placing'),
 ('ManagedBoxMesh.cs','triangles[t + 1] = v + 1;\n            triangles[t + 2] = v + 2;','triangles[t + 1] = v + 2;\n            triangles[t + 2] = v + 1;','box winding'),
 ('ManagedPlacedRenderer.cs','result.IsValid ? _validMaterial : _invalidMaterial','result.IsValid ? _invalidMaterial : _validMaterial','ghost colors swapped'),
 ('ManagedPlacedRenderer.cs','bucket.GetChunkLength(c)','bucket.GetChunk(c).Length','chunk length'),
 ('ManagedPlacedColliders.cs','proxy.gameObject.SetActive(false);','','proxy stays active'),
 ('ManagedPlacedColliders.cs','def.FootprintCellsX * cell, def.HeightMeters','width * cell, def.HeightMeters','collider ignores rotation'),
 ('ManagedBuildAim.cs','if (hoverPieceId >= 0 && pieceDistance > groundDistance)','if (false)','hover behind ground'),
 ('ManagedBuildAim.cs','collider.transform.IsChildOf(IgnoreRoot)','false','ignore root'),
 ('ManagedBuildMode.cs','bool wasPlacing = _controller.Mode == ManagedPlacementMode.Placing;','bool wasPlacing = false;','pickup frame gating'),
 ('ManagedBuildMode.cs','_player.Features |= ManagedCharacterFeature.Cast;','','cast not restored'),
 ('ManagedBuildMode.cs','_cameraRig.CursorHeldByUi = true;','','rig hold not set'),
 ('ManagedBuildMode.cs','if (_castSuppressed && _player != null)','if (_player != null)','cast restored when it was off'),
 ('ManagedBuildMode.cs','!overTray && !_controller.CommitOnRelease','!_controller.CommitOnRelease','click through the tray'),
 ('ManagedBuildMode.cs','_controller.ArmForClick();\n            }\n        }\n\n        private void OnTrayPress','\n            }\n        }\n\n        private void OnTrayPress','bad drop not armed'),
 ('ManagedBuildPanel.cs','ReleaseCapture();\n            }\n\n            _root.style.display','\n            }\n\n            _root.style.display','hide keeps capture'),
 ('ManagedBuildPanel.cs','Screen.height - screenPixels.y','screenPixels.y','Y flip'),
 ('ManagedBuildPanel.cs','if (evt.button != 0 || _activePointer >= 0)','if (evt.button != 0)','double press'),
 ('ManagedBuildInput.cs','keyIndex >= 0 && keyIndex < keyboard.allKeys.Count','true','Key.None guard'),
 ('ManagedBuildUiHost.cs','catch (System.ArgumentException)','catch (System.InvalidCastException)','font failure'),
 ('ManagedBuildUiHost.cs','if (existing == null)','if (true)','duplicate EventSystem'),
]
rigmuts=[('!CursorHeldByUi && Cursor.lockState','Cursor.lockState','rig recapture'),('&& !CursorHeldByUi)','&&true)','rig crosshair')]
def run(bdir, rigfile):
    r=subprocess.run(['dotnet','run','-nologo','-p:BuildingDir='+bdir,'-p:RigFile='+rigfile],cwd=PROJ,capture_output=True,text=True)
    out=r.stdout+r.stderr
    return r.returncode,out
caught=0; total=0; missed=[]
for f,old,new,name in muts:
    d='/tmp/claude-0/mut/Building'
    shutil.rmtree('/tmp/claude-0/mut',ignore_errors=True); shutil.copytree(SRC,d)
    t=open(d+'/'+f).read()
    if old not in t:
        print('SKIP (pattern missing):',name); continue
    open(d+'/'+f,'w').write(t.replace(old,new,1))
    rc,out=run(d,RIG); total+=1
    if rc!=0: caught+=1
    else: missed.append(name)
    print(('caught ' if rc!=0 else 'MISSED ')+name, ('' if rc!=0 else ''))
for old,new,name in rigmuts:
    shutil.rmtree('/tmp/claude-0/mut',ignore_errors=True); shutil.copytree(SRC,'/tmp/claude-0/mut/Building')
    t=open(RIG).read()
    if old not in t: print('SKIP',name); continue
    open('/tmp/claude-0/mut/rig.cs','w').write(t.replace(old,new,1))
    rc,out=run('/tmp/claude-0/mut/Building','/tmp/claude-0/mut/rig.cs'); total+=1
    if rc!=0: caught+=1
    else: missed.append(name)
    print(('caught ' if rc!=0 else 'MISSED ')+name)
print(caught,'of',total,'caught; missed:',missed)
