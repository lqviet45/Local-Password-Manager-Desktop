# Password Manager Browser Extension

This browser extension provides integration between the Password Manager desktop application and web browsers using the Native Messaging API.

## Installation

1. **Load the extension in Chrome/Edge:**
   - Open `chrome://extensions/` (or `edge://extensions/`)
   - Enable "Developer mode"
   - Click "Load unpacked"
   - Select the `BrowserExtension` folder

2. **Register the native messaging host:**
   - The desktop application should automatically register the native messaging manifest
   - Or manually run: `PasswordManager.Desktop.exe --register-native-messaging`

3. **Get your extension ID:**
   - After loading, note the extension ID from `chrome://extensions/`
   - Update the `allowed_origins` in the native messaging manifest with your extension ID

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

## Notes

- The extension requires the desktop application to be running
- Native messaging only works when the extension is loaded as an unpacked extension or published to the Chrome Web Store
- Make sure to update the extension ID in the native messaging manifest

