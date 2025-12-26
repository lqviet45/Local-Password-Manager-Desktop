// Popup script for Password Manager browser extension

document.addEventListener('DOMContentLoaded', () => {
  const statusDiv = document.getElementById('status');
  const fillBtn = document.getElementById('fillBtn');
  const saveBtn = document.getElementById('saveBtn');
  const checkBtn = document.getElementById('checkBtn');

  // Check connection status
  function updateStatus() {
    chrome.runtime.sendMessage({ action: 'ping' }, (response) => {
      if (chrome.runtime.lastError) {
        statusDiv.textContent = 'Disconnected';
        statusDiv.className = 'status disconnected';
        fillBtn.disabled = true;
        saveBtn.disabled = true;
        checkBtn.disabled = true;
      } else {
        statusDiv.textContent = 'Connected';
        statusDiv.className = 'status connected';
        fillBtn.disabled = false;
        saveBtn.disabled = false;
        checkBtn.disabled = false;
      }
    });
  }

  // Auto-fill button
  fillBtn.addEventListener('click', () => {
    chrome.tabs.query({ active: true, currentWindow: true }, (tabs) => {
      chrome.tabs.sendMessage(tabs[0].id, { action: 'fillForm' }, (response) => {
        if (response && response.success) {
          alert('Credentials filled!');
        } else {
          alert('Failed to fill credentials. Make sure you are on a login page.');
        }
      });
    });
  });

  // Save credentials button
  saveBtn.addEventListener('click', () => {
    chrome.tabs.query({ active: true, currentWindow: true }, (tabs) => {
      chrome.tabs.sendMessage(tabs[0].id, { action: 'captureForm' }, (response) => {
        if (response && response.success && response.credentials) {
          chrome.runtime.sendMessage({
            action: 'saveCredentials',
            url: tabs[0].url,
            username: response.credentials.username,
            password: response.credentials.password
          }, (saveResponse) => {
            if (saveResponse && saveResponse.success) {
              alert('Credentials saved!');
            } else {
              alert('Failed to save credentials.');
            }
          });
        } else {
          alert('No credentials found on this page.');
        }
      });
    });
  });

  // Check vault status button
  checkBtn.addEventListener('click', () => {
    chrome.runtime.sendMessage({ action: 'checkVaultLocked' }, (response) => {
      if (response && response.success) {
        if (response.locked) {
          alert('Vault is locked. Please unlock it in the desktop app.');
        } else {
          alert('Vault is unlocked and ready.');
        }
      } else {
        alert('Failed to check vault status.');
      }
    });
  });

  // Update status on load
  updateStatus();
  
  // Update status every 5 seconds
  setInterval(updateStatus, 5000);
});

