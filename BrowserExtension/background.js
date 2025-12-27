// Background service worker for Password Manager browser extension
// HTTP-BASED VERSION - Much simpler than Native Messaging!
// Communicates with desktop app via localhost HTTP API

const API_BASE_URL = 'http://localhost:7777';

let isConnected = false;
let lastStatusCheck = 0;
const STATUS_CHECK_INTERVAL = 5000; // 5 seconds

// ✅ Simple HTTP request helper
async function sendApiRequest(endpoint, method = 'GET', data = null) {
    try {
        const options = {
            method,
            headers: {
                'Content-Type': 'application/json'
            },
            mode: 'cors'
        };

        if (data && method !== 'GET') {
            options.body = JSON.stringify(data);
        }

        const url = method === 'GET' && data
            ? `${API_BASE_URL}${endpoint}?${new URLSearchParams(data)}`
            : `${API_BASE_URL}${endpoint}`;

        console.log(`📤 API Request: ${method} ${endpoint}`, data || '');

        const response = await fetch(url, options);
        const result = await response.json();

        console.log(`📨 API Response:`, result);
        return result;
    } catch (error) {
        console.error('❌ API request failed:', error);

        // Check if it's a connection error
        if (error.message.includes('Failed to fetch') || error.message.includes('NetworkError')) {
            isConnected = false;
            return {
                success: false,
                error: 'Desktop app is not running',
                connectionError: true
            };
        }

        return { success: false, error: error.message };
    }
}

// ✅ Ping desktop app to check if it's running
async function pingDesktopApp() {
    const result = await sendApiRequest('/api/ping');

    if (result.success) {
        if (!isConnected) {
            console.log('✅ Connected to desktop app:', result.message);
            isConnected = true;

            // Notify popup that we're connected
            chrome.runtime.sendMessage({
                action: 'desktopAppConnected',
                data: result
            }).catch(() => {});
        }
        return true;
    } else {
        if (isConnected) {
            console.log('🔌 Lost connection to desktop app');
            isConnected = false;
        }
        return false;
    }
}

// ✅ Get vault status (logged in, locked, etc.)
async function getVaultStatus() {
    const result = await sendApiRequest('/api/status');

    if (result.success) {
        return {
            success: true,
            isLoggedIn: result.isLoggedIn,
            isVaultLocked: result.isVaultLocked,
            userEmail: result.userEmail,
            isPremium: result.isPremium
        };
    } else {
        return {
            success: false,
            isLoggedIn: false,
            isVaultLocked: true,
            error: result.error || 'Desktop app not running'
        };
    }
}

// ✅ Get credentials for a specific URL
async function getCredentials(url) {
    const result = await sendApiRequest('/api/credentials', 'GET', { url });

    if (result.success) {
        return {
            success: true,
            credentials: result.credentials || [],
            locked: result.locked || false
        };
    } else {
        return {
            success: false,
            credentials: [],
            error: result.error || 'Failed to get credentials',
            locked: result.locked || false
        };
    }
}

// ✅ Save new credentials
async function saveCredentials(url, username, password, name = null) {
    const result = await sendApiRequest('/api/credentials', 'POST', {
        url,
        username,
        password,
        name: name || new URL(url).hostname
    });

    if (result.success) {
        console.log('✅ Credentials saved:', result.itemId);
        return {
            success: true,
            itemId: result.itemId,
            message: result.message
        };
    } else {
        return {
            success: false,
            error: result.error || 'Failed to save credentials'
        };
    }
}

// ✅ Fill credentials for a specific vault item
async function fillCredentials(itemId) {
    const result = await sendApiRequest('/api/credentials/fill', 'GET', { itemId });

    if (result.success) {
        return {
            success: true,
            credentials: result.credentials
        };
    } else {
        return {
            success: false,
            error: result.error || 'Failed to fill credentials'
        };
    }
}

// ✅ Listen for messages from popup and content scripts
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
    console.log('📨 Received message:', request.action);

    switch (request.action) {
        case 'ping':
            pingDesktopApp().then(connected => {
                sendResponse({ success: connected });
            });
            return true; // Will respond asynchronously

        case 'checkVaultLocked':
            getVaultStatus().then(sendResponse);
            return true;

        case 'getCredentials':
            getCredentials(request.url).then(sendResponse);
            return true;

        case 'saveCredentials':
            saveCredentials(
                request.url,
                request.username,
                request.password,
                request.name
            ).then(sendResponse);
            return true;

        case 'fillCredentials':
            fillCredentials(request.itemId).then(sendResponse);
            return true;

        default:
            console.warn('❓ Unknown action:', request.action);
            sendResponse({ success: false, error: 'Unknown action' });
    }
});

// ✅ Periodic status check
async function periodicStatusCheck() {
    const now = Date.now();

    // Only check every 5 seconds to avoid spam
    if (now - lastStatusCheck < STATUS_CHECK_INTERVAL) {
        return;
    }

    lastStatusCheck = now;
    await pingDesktopApp();
}

// ✅ Start periodic checks
setInterval(periodicStatusCheck, STATUS_CHECK_INTERVAL);

// ✅ Initial connection check on startup
chrome.runtime.onStartup.addListener(() => {
    console.log('🚀 Extension starting...');
    pingDesktopApp();
});

chrome.runtime.onInstalled.addListener(() => {
    console.log('📦 Extension installed/updated');
    pingDesktopApp();
});

// ✅ Check connection immediately when service worker loads
console.log('🔧 Service worker loaded');
pingDesktopApp();

// ✅ Keep service worker alive (Chrome requirement)
chrome.alarms.create('keepAlive', { periodInMinutes: 1 });
chrome.alarms.onAlarm.addListener((alarm) => {
    if (alarm.name === 'keepAlive') {
        console.log('🔄 Keep-alive ping');
        pingDesktopApp();
    }
});