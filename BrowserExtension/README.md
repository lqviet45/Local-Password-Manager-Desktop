# Password Manager Browser Extension

This browser extension provides **zero-config** integration between the Password Manager desktop application and web browsers using the Native Messaging API.

## 🚀 Zero-Config Installation

**No configuration needed!** Just follow these simple steps:

### Step 1: Download and Install Desktop App

**Option A: Download from Release (Recommended)**
1. Go to the repository Releases page
2. Download `PasswordManager.Desktop.exe` (or installer package)
3. Run the installer or executable
4. Launch Password Manager desktop app
5. Login with your account

**Option B: Build from Source**
1. Clone the repository
2. Open solution in Visual Studio
3. Build `PasswordManager.Desktop` project
4. Run from Visual Studio or find `.exe` in `bin/Release` folder

**Note:** Desktop app will automatically:
- ✅ Register native messaging manifests for Chrome, Edge, Brave
- ✅ Start native messaging host server
- ✅ Be ready to receive connections from browser extension

### Step 2: Install Browser Extension

#### **Option A: Load Extension from Local Machine (Unpacked)**

**For:** Developers, testers, or users who want the latest version

1. **Get Extension Code:**
   - Clone repository or download ZIP
   - Extract and locate `BrowserExtension` folder

2. **Open Browser Extensions Page:**
   - **Chrome**: Go to `chrome://extensions/`
   - **Edge**: Go to `edge://extensions/`
   - **Brave**: Go to `brave://extensions/`

3. **Enable Developer Mode:**
   - Find "Developer mode" toggle in top-right corner
   - Turn it ON

4. **Load Extension:**
   - Click "Load unpacked" button
   - Select the `BrowserExtension` folder from repository
   - Extension will appear in the extensions list

5. **Verify Installation:**
   - Extension icon should appear in browser toolbar
   - Click icon to open popup
   - Check connection status (should show "Connected & Ready" if desktop app is running)

#### **Option B: Install from Chrome Web Store (Production)**

**For:** End users who want easy installation

1. **Find Extension:**
   - Go to Chrome Web Store
   - Search for "Password Manager"
   - Click on the extension

2. **Install:**
   - Click "Add to Chrome" button
   - Confirm installation
   - Extension will be installed automatically

**Note:** If extension is published, the manifest may need to be updated with the actual extension ID. Currently uses wildcard pattern which works with both unpacked and published extensions.

### Step 3: Start Using!

1. **Ensure Desktop App is Running:**
   - Desktop app must be started and logged in
   - Vault must be unlocked

2. **Check Connection:**
   - Click extension icon in toolbar
   - Popup will show status:
     - ✅ "Connected & Ready" - Ready to use
     - ❌ "Desktop app not running" - Start desktop app
     - 🔒 "Vault is locked" - Unlock vault in desktop app

3. **Use Auto-Fill:**
   - Navigate to any login page
   - Press `Ctrl+Shift+L` or click extension → "Auto-Fill Current Page"
   - Credentials will be automatically filled

4. **Use Auto-Save:**
   - Enter credentials on login page
   - Submit form
   - Extension will automatically detect and prompt to save (or auto-save if configured)

## Features

- **Auto-Fill**: Automatically fill login forms with saved credentials
- **Auto-Save**: Capture and save new login credentials
- **Keyboard Shortcut**: Press `Ctrl+Shift+L` on any login page to auto-fill
- **Native Integration**: Secure communication with desktop app via Native Messaging

## Usage

1. **Auto-Fill:**
   - Navigate to a login page
   - Press `Ctrl+Shift+L` or click the extension icon and select "Auto-Fill Current Page"

2. **Save Credentials:**
   - Enter your credentials on a login page
   - Click the extension icon and select "Save Current Credentials"
   - Or the extension will prompt you automatically after form submission

3. **Check Vault Status:**
   - Click the extension icon
   - Click "Check Vault Status" to see if the vault is locked

## Development

### File Structure

- `manifest.json` - Extension manifest
- `background.js` - Service worker for native messaging
- `content.js` - Content script for form detection and filling
- `popup.html/js` - Extension popup UI
- `icon*.png` - Extension icons (you'll need to add these)

### Native Messaging Protocol

Messages are sent as JSON with the following format:

```json
{
  "Type": "GetCredentials|SaveCredentials|FillCredentials|CheckVaultLocked|Ping",
  "MessageId": "unique-id",
  "Data": { ... },
  "Timestamp": "ISO-8601 timestamp"
}
```

Messages are length-prefixed with a 4-byte little-endian integer before the JSON payload.

## 🔧 How It Works

1. **Desktop App Startup:**
   - Automatically registers native messaging manifests for Chrome, Edge, and Brave
   - Starts native messaging host server
   - Ready to receive connections from browser extensions

2. **Extension Startup:**
   - Automatically connects to native messaging host
   - Reconnects automatically if connection is lost
   - Shows connection status in popup

3. **Zero Configuration:**
   - No extension ID needed (uses wildcard pattern)
   - No manual manifest editing
   - Works immediately after installation

## 📝 Notes

- The extension requires the desktop application to be running
- Native messaging only works when the extension is loaded as an unpacked extension or published to the Chrome Web Store
- The extension will automatically reconnect when the desktop app starts
- All communication is secure and local (no internet required)

## 🔧 Troubleshooting

### Extension won't connect to Desktop App:

1. **Check Desktop App:**
   - Make sure desktop app is running
   - Make sure you're logged in and vault is unlocked

2. **Check Native Messaging Manifest:**
   - Open manifest file at:
     - Chrome: `%LocalAppData%\Google\Chrome\User Data\NativeMessagingHosts\com.passwordmanager.json`
     - Edge: `%LocalAppData%\Microsoft\Edge\User Data\NativeMessagingHosts\com.passwordmanager.json`
   - Verify `path` points to correct `.exe` file location

3. **Check Extension Console:**
   - Go to `chrome://extensions/`
   - Click "Inspect views: service worker" on the extension
   - Check console logs for errors

4. **Restart:**
   - Restart desktop app
   - Reload extension (click reload icon on extension card)

### Extension won't fill credentials:

1. **Check if credentials exist:**
   - Open desktop app
   - Find vault item for that URL
   - Make sure username and password are saved

2. **Check Form Detection:**
   - Extension automatically detects username/password fields
   - If form has special structure, may need to update `content.js`

3. **Try Manual Fill:**
   - Click extension icon → "Auto-Fill Current Page"
   - Check for error messages

## 📋 Installation Checklist

- [ ] Downloaded and installed desktop app
- [ ] Started desktop app and logged in
- [ ] Loaded extension into browser (unpacked or from store)
- [ ] Checked extension popup shows "Connected & Ready"
- [ ] Tested auto-fill on a login page
- [ ] Tested auto-save new credentials

