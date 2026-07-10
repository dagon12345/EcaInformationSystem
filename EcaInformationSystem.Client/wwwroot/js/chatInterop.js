window.chatInterop = {
    registerScrollTop: function (elementId, dotNetRef) {
        const el = document.getElementById(elementId);
        if (!el) return;

        let ticking = false;
        el.onscroll = function () {
            if (ticking) return;
            ticking = true;
            requestAnimationFrame(() => {
                if (el.scrollTop < 80) {
                    dotNetRef.invokeMethodAsync('OnScrolledNearTop');
                }
                ticking = false;
            });
        };
    },
    scrollToElement: function (elementId) {
        const el = document.getElementById(elementId);
        if (el) {
            el.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
    },

    preserveScrollAfterPrepend: function (elementId, previousScrollHeight) {
        const el = document.getElementById(elementId);
        if (!el) return;
        el.scrollTop = el.scrollHeight - previousScrollHeight;
    },

    scrollToBottom: function (elementId) {
        const el = document.getElementById(elementId);
        if (el) el.scrollTop = el.scrollHeight;
    },

    getScrollHeight: function (elementId) {
        const el = document.getElementById(elementId);
        return el ? el.scrollHeight : 0;
    },

    _notificationAudio: null,
    _audioUnlocked: false,

    // ✅ NEW — call this once on app startup. Listens for the FIRST click
    // or keypress ANYWHERE on the page (not just the chat widget) and uses
    // that as the browser-required "user gesture" to unlock audio playback.
    // After that, playNotificationSound works immediately for real messages.
    primeNotificationAudio: function () {
        if (this._audioUnlocked) return;

        const unlock = () => {
            if (this._audioUnlocked) return;
            if (!this._notificationAudio) {
                this._notificationAudio = new Audio('audio/notification.mp3');
                this._notificationAudio.volume = 0.5;
            }
            // Play + immediately pause/rewind — this silent "warm-up" play
            // is what satisfies the browser's gesture requirement without
            // actually audibly playing anything to the user.
            this._notificationAudio.play()
                .then(() => {
                    this._notificationAudio.pause();
                    this._notificationAudio.currentTime = 0;
                    this._audioUnlocked = true;
                })
                .catch(() => { /* still blocked — will retry on next gesture */ });

            document.removeEventListener('click', unlock);
            document.removeEventListener('keydown', unlock);
        };

        document.addEventListener('click', unlock);
        document.addEventListener('keydown', unlock);
    },

    playNotificationSound: function () {
        try {
            if (!this._notificationAudio) {
                this._notificationAudio = new Audio('audio/notification.mp3');
                this._notificationAudio.volume = 0.5;
            }
            this._notificationAudio.currentTime = 0;
            this._notificationAudio.play().catch(() => { /* autoplay still blocked, non-fatal */ });
        } catch { /* non-fatal */ }
    },
    _outsideClickHandler: null,

    registerOutsideClick: function (containerId, dotNetRef) {
        this.unregisterOutsideClick();

        this._outsideClickHandler = function (event) {
            const container = document.getElementById(containerId);
            if (!container) return;

            // ✅ Use composedPath() instead of container.contains(event.target).
            // composedPath() is captured at dispatch time, so it stays accurate
            // even if Blazor's own click handling removes/replaces the clicked
            // element from the DOM before this bubbles up to document level.
            const path = event.composedPath ? event.composedPath() : [];

            if (!path.includes(container)) {
                dotNetRef.invokeMethodAsync('OnClickOutside');
            }
        };

        setTimeout(() => {
            document.addEventListener('click', this._outsideClickHandler);
        }, 0);
    },

    unregisterOutsideClick: function () {
        if (this._outsideClickHandler) {
            document.removeEventListener('click', this._outsideClickHandler);
            this._outsideClickHandler = null;
        }
    },

    notifications: {
        requestPermission: function () {
            if (!('Notification' in window)) return 'unsupported';
            if (Notification.permission === 'default') {
                Notification.requestPermission();
            }
            return Notification.permission;
        },

        // ✅ Only fires if the tab is NOT currently focused — no point popping
        // an OS notification for something the user is already looking at.
        show: function (title, body, iconUrl) {
            if (!('Notification' in window)) return;
            if (Notification.permission !== 'granted') return;
            if (document.hasFocus()) return;

            const notification = new Notification(title, {
                body: body,
                icon: iconUrl || '/images/ncsc-seal.png',
                tag: 'eca-chat' // ✅ reuses the same notification slot instead of stacking many
            });

            notification.onclick = function () {
                window.focus();
                notification.close();
            };
        }
    },
    _reactionPickerOutsideHandler: null,

    registerReactionPickerOutsideClick: function (dotNetRef) {
        this.unregisterReactionPickerOutsideClick();

        this._reactionPickerOutsideHandler = function (event) {<script src="js/chatInterop.js?v=3"></script>
            const path = event.composedPath ? event.composedPath() : [];
            const clickedInsidePicker = path.some(el =>
                el.classList && el.classList.contains('chat-reaction-picker-floating'));
            const clickedEmojiButton = path.some(el =>
                el.classList && el.classList.contains('chat-emoji-corner-btn'));

            // Close unless the click was on the picker itself OR the button
            // that toggles it (the button already has its own toggle logic).
            if (!clickedInsidePicker && !clickedEmojiButton) {
                dotNetRef.invokeMethodAsync('OnReactionPickerOutsideClick');
            }
        };

        setTimeout(() => {
            document.addEventListener('click', this._reactionPickerOutsideHandler);
        }, 0);
    },

    unregisterReactionPickerOutsideClick: function () {
        if (this._reactionPickerOutsideHandler) {
            document.removeEventListener('click', this._reactionPickerOutsideHandler);
            this._reactionPickerOutsideHandler = null;
        }
    }
};