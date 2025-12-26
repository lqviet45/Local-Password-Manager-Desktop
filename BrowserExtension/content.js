// Content script for Password Manager browser extension
// Handles form detection, auto-fill, and credential capture

(function() {
  'use strict';

  // Configuration
  const CONFIG = {
    autoFillEnabled: true,
    autoSaveEnabled: true,
    highlightFields: true
  };

  // State
  let detectedFields = {
    username: null,
    password: null,
    submit: null
  };

  // Initialize
  function init() {
    detectFormFields();
    setupFormListeners();
    
    if (CONFIG.highlightFields) {
      highlightDetectedFields();
    }

    // Listen for messages from background script
    chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
      if (request.action === 'fillForm') {
        fillForm(request.credentials);
        sendResponse({ success: true });
      } else if (request.action === 'captureForm') {
        const credentials = captureFormData();
        sendResponse({ success: true, credentials });
      }
      return true;
    });
  }

  // Detect form fields
  function detectFormFields() {
    // Find username/email fields
    const usernameSelectors = [
      'input[type="email"]',
      'input[type="text"][name*="user" i]',
      'input[type="text"][name*="email" i]',
      'input[type="text"][name*="login" i]',
      'input[type="text"][id*="user" i]',
      'input[type="text"][id*="email" i]',
      'input[type="text"][id*="login" i]',
      'input[autocomplete="username"]',
      'input[autocomplete="email"]'
    ];

    // Find password fields
    const passwordSelectors = [
      'input[type="password"]',
      'input[name*="pass" i]',
      'input[id*="pass" i]',
      'input[autocomplete="current-password"]',
      'input[autocomplete="new-password"]'
    ];

    // Find submit buttons
    const submitSelectors = [
      'button[type="submit"]',
      'input[type="submit"]',
      'button:not([type])',
      'form button[type="button"]'
    ];

    // Try to find username field
    for (const selector of usernameSelectors) {
      const field = document.querySelector(selector);
      if (field && field.offsetParent !== null) {
        detectedFields.username = field;
        break;
      }
    }

    // Try to find password field
    for (const selector of passwordSelectors) {
      const field = document.querySelector(selector);
      if (field && field.offsetParent !== null) {
        detectedFields.password = field;
        break;
      }
    }

    // Try to find submit button
    for (const selector of submitSelectors) {
      const field = document.querySelector(selector);
      if (field && field.offsetParent !== null) {
        detectedFields.submit = field;
        break;
      }
    }

    console.log('Detected fields:', detectedFields);
  }

  // Setup form listeners
  function setupFormListeners() {
    if (CONFIG.autoSaveEnabled && detectedFields.password) {
      // Listen for form submission
      const form = detectedFields.password.closest('form');
      if (form) {
        form.addEventListener('submit', handleFormSubmit, true);
      }

      // Also listen for password field changes
      detectedFields.password.addEventListener('input', debounce(handlePasswordInput, 1000));
    }
  }

  // Handle form submission
  function handleFormSubmit(event) {
    const credentials = captureFormData();
    if (credentials.username || credentials.password) {
      // Send to background script for saving
      chrome.runtime.sendMessage({
        action: 'saveCredentials',
        url: window.location.href,
        username: credentials.username,
        password: credentials.password
      });
    }
  }

  // Handle password input (for auto-save detection)
  function handlePasswordInput() {
    // This can be used to detect when user is typing password
    // and potentially trigger auto-save prompt
  }

  // Capture form data
  function captureFormData() {
    const credentials = {
      username: null,
      password: null
    };

    if (detectedFields.username) {
      credentials.username = detectedFields.username.value || null;
    }

    if (detectedFields.password) {
      credentials.password = detectedFields.password.value || null;
    }

    return credentials;
  }

  // Fill form with credentials
  function fillForm(credentials) {
    if (!credentials) {
      console.warn('No credentials provided for auto-fill');
      return;
    }

    // Fill username
    if (credentials.username && detectedFields.username) {
      detectedFields.username.value = credentials.username;
      detectedFields.username.dispatchEvent(new Event('input', { bubbles: true }));
      detectedFields.username.dispatchEvent(new Event('change', { bubbles: true }));
    }

    // Fill password
    if (credentials.password && detectedFields.password) {
      detectedFields.password.value = credentials.password;
      detectedFields.password.dispatchEvent(new Event('input', { bubbles: true }));
      detectedFields.password.dispatchEvent(new Event('change', { bubbles: true }));
    }

    console.log('Form filled with credentials');
  }

  // Highlight detected fields (for debugging/visual feedback)
  function highlightDetectedFields() {
    if (detectedFields.username) {
      detectedFields.username.style.outline = '2px solid #4CAF50';
    }
    if (detectedFields.password) {
      detectedFields.password.style.outline = '2px solid #2196F3';
    }
  }

  // Debounce helper
  function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
      const later = () => {
        clearTimeout(timeout);
        func(...args);
      };
      clearTimeout(timeout);
      timeout = setTimeout(later, wait);
    };
  }

  // Request credentials for current page
  function requestCredentials() {
    chrome.runtime.sendMessage({
      action: 'getCredentials',
      url: window.location.href
    }, (response) => {
      if (response && response.success && response.credentials) {
        fillForm(response.credentials);
      }
    });
  }

  // Keyboard shortcut handler (Ctrl+Shift+L for auto-fill)
  document.addEventListener('keydown', (event) => {
    if (event.ctrlKey && event.shiftKey && event.key === 'L') {
      event.preventDefault();
      requestCredentials();
    }
  });

  // Initialize when DOM is ready
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }

  // Re-detect fields when page changes (for SPAs)
  let lastUrl = location.href;
  new MutationObserver(() => {
    const url = location.href;
    if (url !== lastUrl) {
      lastUrl = url;
      detectFormFields();
      if (CONFIG.highlightFields) {
        highlightDetectedFields();
      }
    }
  }).observe(document, { subtree: true, childList: true });

})();

