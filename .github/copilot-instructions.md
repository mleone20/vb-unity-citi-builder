# Project Guidelines

## Architecture
This workspace folder is a module inside the larger Unity project rooted at d:/Unity/DevAmbient1.
Prefer simple MonoBehaviour-driven implementations over adding new architectural layers unless the task explicitly requires them.

## Code Style
Use C# scripts that fit Unity defaults: PascalCase types and methods, MonoBehaviour lifecycle methods, and serialized inspector-facing fields when designers need to wire references in the Editor.
Do not introduce namespaces, asmdef files, or editor tooling abstractions unless the task requires them and the surrounding project is updated to support them.
Keep changes compatible with the current single-assembly setup that builds into Assembly-CSharp.

## Build And Test
Target Unity Editor 6000.4.0f1.
No project-local automated test setup was found for this module.
When validating code changes, prefer Unity-safe checks: confirm scripts compile cleanly in the Editor and watch the Unity Console for serialization or missing-reference errors.
Assume HDRP is part of the environment because the project depends on com.unity.render-pipelines.high-definition.

## Conventions
Prefer inspector-assigned references over hard-coded scene lookups when the object relationship is known in advance.
When touching existing gameplay code, preserve the project's pragmatic component patterns such as direct GetComponent calls and small focused scripts instead of refactoring broadly.
If you add new scripts under this workspace folder, keep them self-contained and safe for Unity serialization changes such as renaming public fields.