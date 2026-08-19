# Fallout Angles

Fallout Angles is a Unity HDRP game project targeting Unity `6000.5.0f1`.

## Requirements

- Unity `6000.5.0f1`
- Git LFS 3.x or newer

## Getting started

```powershell
git lfs install
git clone https://github.com/atritheone/FalloutAngles.git
```

Open the cloned repository directory as a Unity project. Unity will recreate
`Library`, `Temp`, generated IDE projects, and other local state on first open.

## Local character assets

Human Generator source and derived assets are deliberately excluded from this
repository. This includes the player/NPC source models, generated skinned
attachments, extracted player hair textures and material, and the generated
player avatar.

The project-owned player and NPC prefabs remain in source control, so their
model, mesh, texture, and avatar references will be missing on a clean clone.
Use redistributable replacement characters, or restore legitimately licensed
local assets without committing them.

The relevant ignored locations are documented in `.gitignore`.

## Repository layout

- `Assets/` contains game code, scenes, prefabs, definitions, and content.
- `Packages/` contains the Unity package manifest and the embedded UniGLTF
  dependency.
- `ProjectSettings/` contains shared Unity project configuration.

Large animation, model, image, font, and audio files are stored with Git LFS.

## Licensing

No licence is granted for the original project code or content unless a file
explicitly states otherwise. Third-party components retain their respective
licences; see `THIRD_PARTY_NOTICES.md`.
