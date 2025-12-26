// Background service worker for Password Manager browser extension
// Handles native messaging communication with desktop app

const NATIVE_HOST_NAME = 'com.passwordmanager';
let nativePort = null;
let reconnectAttempts = 0;
const MAX_RECONNECT_ATTEMPTS = 3;

// Message queue for when native host is not connected
const messageQueue = [];

// Connect to native messaging host
function connectNativeHost() {
  try {
    nativePort = chrome.runtime.connectNative(NATIVE_HOST_NAME);
    
    nativePort.onMessage.addListener((message) => {
      handleNativeMessage(message);
    });

    nativePort.onDisconnect.addListener(() => {
      console.log('Native host disconnected');
      nativePort = null;
      
      if (chrome.runtime.lastError) {
        console.error('Native messaging error:', chrome.runtime.lastError.message);
      }

      // Attempt to reconnect
      if (reconnectAttempts < MAX_RECONNECT_ATTEMPTS) {
        reconnectAttempts++;
        setTimeout(() => {
          connectNativeHost();
        }, 1000 * reconnectAttempts);
      }
    });

    reconnectAttempts = 0;
    console.log('Connected to native messaging host');

    // Process queued messages
    while (messageQueue.length > 0) {
      const message = messageQueue.shift();
      sendNativeMessage(message);
    }
  } catch (error) {
    console.error('Failed to connect to native host:', error);
  }
}

// Send message to native host
function sendNativeMessage(message) {
  if (!nativePort) {
    console.warn('Native port not connected, queueing message');
    messageQueue.push(message);
    connectNativeHost();
    return;
  }

  try {
    nativePort.postMessage(message);
  } catch (error) {
    console.error('Failed to send message to native host:', error);
    messageQueue.push(message);
  }
}

// Handle messages from native host
function handleNativeMessage(message) {
  console.log('Received message from native host:', message);

  switch (message.Type) {
    case 'Pong':
      console.log('Received pong from native host');
      break;
    case 'Credentials':
      handleCredentialsResponse(message);
      break;
    case 'VaultLocked':
      handleVaultLockedResponse(message);
      break;
    default:
      console.log('Unknown message type:', message.Type);
  }
}

// Handle credentials response
function handleCredentialsResponse(message) {
  // Store credentials temporarily for content script
  chrome.storage.local.set({
    credentials: message.Data,
    timestamp: Date.now()
  });
}

// Handle vault locked response
function handleVaultLockedResponse(message) {
  chrome.storage.local.set({
    vaultLocked: message.Data?.locked || false
  });
}

// Listen for messages from content script or popup
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  console.log('Received message:', request);

  switch (request.action) {
    case 'getCredentials':
      handleGetCredentials(request, sendResponse);
      return true; // Will respond asynchronously

    case 'saveCredentials':
      handleSaveCredentials(request, sendResponse);
      return true;

    case 'fillCredentials':
      handleFillCredentials(request, sendResponse);
      return true;

    case 'checkVaultLocked':
      handleCheckVaultLocked(sendResponse);
      return true;

    case 'ping':
      sendNativeMessage({
        Type: 'Ping',
        MessageId: generateMessageId(),
        Data: null,
        Timestamp: new Date().toISOString()
      });
      sendResponse({ success: true });
      break;

    default:
      sendResponse({ success: false, error: 'Unknown action' });
  }
});

// Handle get credentials request
function handleGetCredentials(request, sendResponse) {
  const message = {
    Type: 'GetCredentials',
    MessageId: generateMessageId(),
    Data: {
      url: request.url || sender?.tab?.url
    },
    Timestamp: new Date().toISOString()
  };

  sendNativeMessage(message);

  // Wait for response (simplified - in production, use proper message correlation)
  setTimeout(() => {
    chrome.storage.local.get(['credentials'], (result) => {
      sendResponse({ success: true, credentials: result.credentials });
    });
  }, 500);
}

// Handle save credentials request
function handleSaveCredentials(request, sendResponse) {
  const message = {
    Type: 'SaveCredentials',
    MessageId: generateMessageId(),
    Data: {
      url: request.url,
      username: request.username,
      password: request.password,
      name: request.name
    },
    Timestamp: new Date().toISOString()
  };

  sendNativeMessage(message);
  sendResponse({ success: true });
}

// Handle fill credentials request
function handleFillCredentials(request, sendResponse) {
  const message = {
    Type: 'FillCredentials',
    MessageId: generateMessageId(),
    Data: {
      vaultItemId: request.vaultItemId,
      url: request.url
    },
    Timestamp: new Date().toISOString()
  };

  sendNativeMessage(message);
  sendResponse({ success: true });
}

// Handle check vault locked request
function handleCheckVaultLocked(sendResponse) {
  const message = {
    Type: 'CheckVaultLocked',
    MessageId: generateMessageId(),
    Data: null,
    Timestamp: new Date().toISOString()
  };

  sendNativeMessage(message);

  setTimeout(() => {
    chrome.storage.local.get(['vaultLocked'], (result) => {
      sendResponse({ success: true, locked: result.vaultLocked || false });
    });
  }, 500);
}

// Generate unique message ID
function generateMessageId() {
  return `${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
}

// Initialize connection on startup
chrome.runtime.onStartup.addListener(() => {
  connectNativeHost();
});

chrome.runtime.onInstalled.addListener(() => {
  connectNativeHost();
});

// Connect immediately
connectNativeHost();

