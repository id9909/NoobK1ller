# Noob K1ller (Lite)

Android Unity IL2CPP anti-cheat. Encrypts `global-metadata.dat` and breaking runtime dumping tools.

## Features

### Encrypt Metadata
Encrypts main sections of `global-metadata.dat`. Dumping tools like Il2CppDumper will not be able to parse the metadata.

### Encrypt Keys
Adds oxorany obfuscation to all section decryption keys. Requires C++14. Reversing metadata becomes harder.

### Rename IL2CPP Exports
Randomizes all IL2CPP exported function names. Instead of `il2cpp_domain_get`, imports will look like `rTneAkhDlwQ`, breaking 99% of runtime dumpers.

### Strip Mono Symbols
Removes unused Mono imports that hackers can use for runtime dumping.

## Installation

Download the latest `.unitypackage` from [Releases](../../releases).

Import it into your Unity project and configure the protection settings before building.

## Usage

1. Import the package
2. Open the config window via `Tools → Noob K1ller сonfig`
3. Select protection methods
4. Build your Android IL2CPP project as usual

The protection is applied automatically during the build process.

## Full Version

This is the lite version with basic protection. The full version includes additional layers and more runtime protection (such as IL2CPP API call verification).

**Get the full version:** Contact me via any social link listed on my GitHub profile.

## Requirements

- Unity 2022.3+
- Android IL2CPP build target
