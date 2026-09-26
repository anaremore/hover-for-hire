# macOS distribution

The macOS player is a universal app (Intel and Apple silicon). Unity signs it ad hoc when it builds, including builds made on Windows: both architectures of `Contents/MacOS/Hover for Hire` carry a code signature, and the bundle has `_CodeSignature/CodeResources`. An ad-hoc signature lets the app run on Apple silicon. It does not identify a developer, so Gatekeeper blocks a copy that was downloaded.

## Running an unsigned download (players)

Extract the archive and move **Hover for Hire.app** to Applications. Then either:
- open it once, then go to **System Settings → Privacy & Security** and choose **Open Anyway**. (On macOS 14 and earlier, Control-click the app and choose **Open**.)
- or remove the quarantine flag in Terminal:

  ```sh
  xattr -dr com.apple.quarantine "/Applications/Hover for Hire.app"
  ```

## Signing and notarizing (maintainers, on a Mac)

This needs an Apple Developer Program membership and a **Developer ID Application** certificate in the keychain. These are Unity's documented steps.

1. **Entitlements.** Save the minimum Hardened Runtime entitlements Unity lists as `HoverForHire.entitlements`:

   ```xml
   <?xml version="1.0" encoding="UTF-8"?>
   <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
   <plist version="1.0">
       <dict>
           <key>com.apple.security.cs.disable-library-validation</key>
           <true/>
           <key>com.apple.security.cs.disable-executable-page-protection</key>
           <true/>
       </dict>
   </plist>
   ```

2. **Sign** with the Hardened Runtime and a secure timestamp:

   ```sh
   codesign --deep --force --verify --verbose --timestamp --options runtime \
     --entitlements HoverForHire.entitlements \
     --sign "Developer ID Application: NAME (TEAMID)" "Hover for Hire.app"
   ```

3. **Zip and notarize.** Apple's `notarytool` needs Xcode 13 or later. Store the credentials once with `xcrun notarytool store-credentials`, using an app-specific password.

   ```sh
   ditto -c -k --sequesterRsrc --keepParent "Hover for Hire.app" "Hover for Hire.zip"
   xcrun notarytool submit "Hover for Hire.zip" --keychain-profile "hover-for-hire" --wait
   ```

4. **Staple** the ticket to the app, then zip the stapled app for distribution:

   ```sh
   xcrun stapler staple "Hover for Hire.app"
   ```

Finally, run `Tools/package_builds.py --allow-stale` on the signed app. The signing step does not change the recorded commit, but it happens after the build.

## Signing without a Mac

[rcodesign](https://github.com/indygreg/apple-platform-rs) (apple-codesign) signs Mach-O binaries and bundles, and submits them for notarization through the App Store Connect API, on Windows and Linux. It could sign the Windows-built player with a Developer ID certificate exported as a `.p12` file. It isn't part of this project's tooling: download and use it only if you choose to.

## Still to verify on real hardware

These builds have not yet been run on a Mac:
- launch on Intel and on Apple silicon;
- controller mapping;
- audio;
- saves, which go to `~/Library/Application Support`;
- the Command-key shortcuts.

See the platform checklist in [Validation.md](Validation.md).
