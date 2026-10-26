"""Generate original geometric prototype art; no source artwork is read or copied."""
from pathlib import Path
import hashlib
import re
import struct
import zlib

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / 'Assets/Dungonator/Client/Layers/Maze/Floors/Tiles/Original'
WALLS = [10, 5, 12, 9, 3, 6, 0, 1, 2, 4, 8, 14, 13, 11, 7, 15]


def guid(name):
    return hashlib.sha256(('Dungonator original placeholder ' + name).encode()).hexdigest()[:32]


def png(path, pixels, size=16):
    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data))
    raw = b''.join(b'\0' + bytes(sum(pixels[y * size:(y + 1) * size], ())) for y in range(size))
    path.write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>2I5B', size, size, 8, 6, 0, 0, 0))
                     + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))


def sprite_meta(path, identity):
    path.write_text(f'''fileFormatVersion: 2
guid: {identity}
TextureImporter:
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  textureSettings:
    filterMode: 0
    aniso: 1
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  spriteMode: 1
  spriteMeshType: 0
  spritePixelsToUnits: 16
  spritePivot: {{x: 0.5, y: 0.5}}
  alphaIsTransparency: 1
  textureType: 8
  textureShape: 1
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
  userData:
  assetBundleName:
  assetBundleVariant:
''', encoding='utf-8')


def tile(name):
    identity = guid(name + '.asset')
    (ART / (name + '.asset.meta')).write_text(f'fileFormatVersion: 2\nguid: {identity}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n', encoding='utf-8')
    (ART / (name + '.asset')).write_text(f'''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 13312, guid: 0000000000000000e000000000000000, type: 0}}
  m_Name: {name}
  m_EditorClassIdentifier:
  m_Sprite: {{fileID: 21300000, guid: {guid(name + '.png')}, type: 3}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_Transform:
    e00: 1
    e01: 0
    e02: 0
    e03: 0
    e10: 0
    e11: 1
    e12: 0
    e13: 0
    e20: 0
    e21: 0
    e22: 1
    e23: 0
    e30: 0
    e31: 0
    e32: 0
    e33: 1
  m_InstancedGameObject: {{fileID: 0}}
  m_Flags: 1
  m_ColliderType: 0
''', encoding='utf-8')
    return identity


def wall_at(x, y, mask, size=10, thickness=3):
    # Wall stripes plus corner posts match the existing maze's directional topology.
    if (mask & 1 and y >= size-thickness) or (mask & 2 and x >= size-thickness): return True
    if (mask & 4 and y < thickness) or (mask & 8 and x < thickness): return True
    return (x < thickness or x >= size-thickness) and (y < thickness or y >= size-thickness)


def main():
    ART.mkdir(parents=True, exist_ok=True)
    ART.with_suffix('.meta').write_text(f'fileFormatVersion: 2\nguid: {guid("Original folder")}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n', encoding='utf-8')
    for name in ['Floor', 'Wall']:
        pixels=[]
        for y in range(16):
            for x in range(16):
                if name=='Floor':
                    c = (43, 53, 62, 255) if x in (0,15) or y in (0,15) else (54+(x+y)%3, 65+(x+y)%3, 74+(x+y)%3, 255)
                else:
                    mortar = y%8==0 or (x+(8 if y>=8 else 0))%16==0
                    c = (22, 29, 36, 255) if mortar else ((113, 137, 150, 255) if y%8==1 else (77, 98, 113, 255))
                pixels.append(c)
        png(ART/(name+'.png'), pixels)
        sprite_meta(ART/(name+'.png.meta'),guid(name+'.png'))
    floor, wall = tile('Floor'), tile('Wall')
    templates=ROOT/'Assets/Resources/CellTemplates/Layer1'
    for p in templates.glob('*.asset'):
        text=p.read_text(encoding='utf-8-sig');shape=int(re.search(r'^  shape: (\d+)',text,re.M)[1])
        prefix=text[:text.index('  flatLayout:')]
        prefix=re.sub(r'^  (cellSizeXY|wallThickness):.*\n', '', prefix, flags=re.M)
        cells=[wall if wall_at(x,y,WALLS[shape]) else floor for y in range(10) for x in range(10)]
        p.write_text(prefix+'  cellSizeXY: 10\n  wallThickness: 3\n  flatLayout:\n'+''.join(f'  - {{fileID: 11400000, guid: {g}, type: 2}}\n' for g in cells),encoding='utf-8')
    player=ROOT/'Assets/Resources/Player/debugplayer.png'
    pixels=[((241,193,74,255) if abs(x-7.5)+abs(y-7.5)<7 else (0,0,0,0)) for y in range(16) for x in range(16)]
    png(player,pixels)
    sprite_meta(player.with_suffix('.png.meta'),'9c3f4413ec2db034ab6749d26b4e5e17')
    print('Generated original floor/wall/player art and 16 maze templates.')


if __name__=='__main__': main()
