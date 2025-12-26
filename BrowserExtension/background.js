// Background service worker for Password Manager browser extension
// Handles native messaging communication with desktop app
// Zero-config: Works automatically when desktop app is running

const NATIVE_HOST_NAME = 'com.passwordmanager';
let nativePort = null;
let reconnectAttempts = 0;
const MAX_RECONNECT_ATTEMPTS = 5;
let isConnected = false;

// Message queue for when native host is not connected
const messageQueue = [];

// Store response callbacks for async message handling
const responseCallbacks = {};

// Connect to native messaging host
function connectNativeHost() {
  try {
    nativePort = chrome.runtime.connectNative(NATIVE_HOST_NAME);
    isConnected = true;
    reconnectAttempts = 0;
    
    nativePort.onMessage.addListener((message) => {
      handleNativeMessage(message);
    });

    nativePort.onDisconnect.addListener(() => {
      console.log('Native host disconnected');
      nativePort = null;
      isConnected = false;
      
      if (chrome.runtime.lastError) {
        const errorMsg = chrome.runtime.lastError.message;
        console.error('Native messaging error:', errorMsg);
        
        // Don't reconnect if host not found (app not running)
        if (errorMsg.includes('Specified native messaging host not found')) {
          console.log('Desktop app is not running. Extension will work when app starts.');
          return;
        }
      }

      // Attempt to reconnect if it was a connection error
      if (reconnectAttempts < MAX_RECONNECT_ATTEMPTS) {
        reconnectAttempts++;
        console.log(`Attempting to reconnect (${reconnectAttempts}/${MAX_RECONNECT_ATTEMPTS})...`);
        setTimeout(() => {
          connectNativeHost();
        }, 2000 * reconnectAttempts); // Exponential backoff
      } else {
        console.log('Max reconnection attempts reached. Please ensure desktop app is running.');
      }
    });

    console.log('✅ Connected to Password Manager desktop app');

    // Process queued messages
    while (messageQueue.length > 0) {
      const message = messageQueue.shift();
      sendNativeMessage(message);
    }

    // Notify content scripts that connection is ready
    chrome.runtime.sendMessage({ action: 'nativeHostConnected' }).catch(() => {
      // Ignore if no listeners
    });
  } catch (error) {
    console.error('Failed to connect to native host:', error);
    isConnected = false;
    
    // Retry after delay
    if (reconnectAttempts < MAX_RECONNECT_ATTEMPTS) {
      reconnectAttempts++;
      setTimeout(() => {
        connectNativeHost();
      }, 3000);
    }
  }
}

// Send message to native host
function sendNativeMessage(message) {
  if (!nativePort || !isConnected) {
    console.warn('Native port not connected, queueing message');
    messageQueue.push(message);
    
    // Try to reconnect if not already trying
    if (reconnectAttempts === 0) {
      connectNativeHost();
    }
    return;
  }

  try {
    nativePort.postMessage(message);
  } catch (error) {
    console.error('Failed to send message to native host:', error);
    messageQueue.push(message);
    isConnected = false;
  }
}

// Handle messages from native host
function handleNativeMessage(message) {
  console.log('Received message from native host:', message);

  // Check if this is a response to a pending request
  if (message.MessageId && responseCallbacks[message.MessageId]) {
    const callback = responseCallbacks[message.MessageId];
    delete responseCallbacks[message.MessageId];
    
    // Format response based on message type
    const response = {
      success: message.Data?.success !== false,
      ...message.Data
    };
    
    callback(response);
    return;
  }

  // Handle unsolicited messages
  switch (message.Type) {
    case 'Pong':
      console.log('Received pong from native host');
      break;
    case 'GetCredentials':
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
  if (!isConnected) {
    sendResponse({ 
      success: false, 
      error: 'Desktop app is not running. Please start Password Manager desktop app.' 
    });
    return;
  }

  const message = {
    Type: 'GetCredentials',
    MessageId: generateMessageId(),
    Data: {
      url: request.url
    },
    Timestamp: new Date().toISOString()
  };

  // Store callback for response
  const messageId = message.MessageId;
  responseCallbacks[messageId] = sendResponse;

  sendNativeMessage(message);

  // Timeout after 5 seconds
  setTimeout(() => {
    if (responseCallbacks[messageId]) {
      delete responseCallbacks[messageId];
      sendResponse({ 
        success: false, 
        error: 'Timeout waiting for response from desktop app' 
      });
    }
  }, 5000);
}

// Handle save credentials request
function handleSaveCredentials(request, sendResponse) {
  if (!isConnected) {
    sendResponse({ 
      success: false, 
      error: 'Desktop app is not running. Please start Password Manager desktop app.' 
    });
    return;
  }

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

  // Store callback for response
  const messageId = message.MessageId;
  responseCallbacks[messageId] = sendResponse;

  sendNativeMessage(message);

  // Timeout after 5 seconds
  setTimeout(() => {
    if (responseCallbacks[messageId]) {
      delete responseCallbacks[messageId];
      sendResponse({ 
        success: false, 
        error: 'Timeout waiting for response from desktop app' 
      });
    }
  }, 5000);
}

// Handle fill credentials request
function handleFillCredentials(request, sendResponse) {
  if (!isConnected) {
    sendResponse({ 
      success: false, 
      error: 'Desktop app is not running. Please start Password Manager desktop app.' 
    });
    return;
  }

  const message = {
    Type: 'FillCredentials',
    MessageId: generateMessageId(),
    Data: {
      vaultItemId: request.vaultItemId,
      url: request.url
    },
    Timestamp: new Date().toISOString()
  };

  // Store callback for response
  const messageId = message.MessageId;
  responseCallbacks[messageId] = sendResponse;

  sendNativeMessage(message);

  // Timeout after 5 seconds
  setTimeout(() => {
    if (responseCallbacks[messageId]) {
      delete responseCallbacks[messageId];
      sendResponse({ 
        success: false, 
        error: 'Timeout waiting for response from desktop app' 
      });
    }
  }, 5000);
}

// Handle check vault locked request
function handleCheckVaultLocked(sendResponse) {
  if (!isConnected) {
    sendResponse({ 
      success: false, 
      locked: true,
      error: 'Desktop app is not running' 
    });
    return;
  }

  const message = {
    Type: 'CheckVaultLocked',
    MessageId: generateMessageId(),
    Data: null,
    Timestamp: new Date().toISOString()
  };

  // Store callback for response
  const messageId = message.MessageId;
  responseCallbacks[messageId] = sendResponse;

  sendNativeMessage(message);

  // Timeout after 5 seconds
  setTimeout(() => {
    if (responseCallbacks[messageId]) {
      delete responseCallbacks[messageId];
      sendResponse({ 
        success: false, 
        locked: true,
        error: 'Timeout waiting for response from desktop app' 
      });
    }
  }, 5000);
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

