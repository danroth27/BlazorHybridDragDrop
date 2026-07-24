// Shared drag-and-drop diagnostics + workaround helpers for dotnet/maui#2205.
// The key diagnostic value here is capturing which native HTML5 DnD events actually
// fire on each host/platform, since the reported symptom is "dragstart fires but
// nothing else does".

window.dragDropTest = (function () {
    let dotnet = null;
    const rawEvents = ['dragstart', 'drag', 'dragenter', 'dragover', 'dragleave', 'drop', 'dragend'];

    function log(msg) {
        try {
            if (dotnet) {
                dotnet.invokeMethodAsync('OnJsLog', msg);
            }
        } catch (e) {
            console.error('dragDropTest.log failed', e);
        }
    }

    return {
        init: function (dotNetRef) {
            dotnet = dotNetRef;
        },

        // Attach raw listeners so we can see exactly which events reach the DOM.
        // preventDefault on dragover/drop is required by the HTML5 spec for drop to fire.
        observe: function (id) {
            const el = document.getElementById(id);
            if (!el) {
                log('observe: element not found: ' + id);
                return;
            }
            rawEvents.forEach(function (ev) {
                el.addEventListener(ev, function (e) {
                    if (ev === 'dragover' || ev === 'drop' || ev === 'dragenter') {
                        e.preventDefault();
                    }
                    let extra = '';
                    if (e.dataTransfer) {
                        const files = e.dataTransfer.files;
                        if (files && files.length) {
                            extra += ' files=[' + Array.from(files).map(function (f) { return f.name; }).join(', ') + ']';
                        }
                        try {
                            const txt = e.dataTransfer.getData('text');
                            if (txt) extra += ' text="' + txt + '"';
                        } catch (_) { /* getData may throw outside drop */ }
                    }
                    log('[' + id + '] ' + ev + extra);
                }, false);
            });
            log('observing: ' + id);
        },

        environment: function () {
            return {
                userAgent: navigator.userAgent,
                platform: navigator.platform,
                maxTouchPoints: navigator.maxTouchPoints || 0,
                pointerEvents: !!window.PointerEvent,
                dragEventInWindow: ('ondragstart' in window)
            };
        },

        // Minimal pointer-based reordering. This is the recommended workaround: it does
        // NOT use HTML5 drag events at all, so it works on touch platforms (Android/iOS)
        // and inside WebView2 regardless of the native DnD limitation.
        pointerSortable: function (containerId) {
            const container = document.getElementById(containerId);
            if (!container) { log('pointerSortable: container not found'); return; }
            let dragEl = null, placeholder = null, startY = 0, offsetY = 0;

            function onPointerDown(e) {
                const item = e.target.closest('[data-ps-item]');
                if (!item || !container.contains(item)) return;
                dragEl = item;
                dragEl.setPointerCapture(e.pointerId);
                const rect = dragEl.getBoundingClientRect();
                offsetY = e.clientY - rect.top;
                startY = e.clientY;
                placeholder = dragEl.cloneNode(false);
                placeholder.style.visibility = 'hidden';
                dragEl.parentNode.insertBefore(placeholder, dragEl.nextSibling);
                dragEl.style.position = 'fixed';
                dragEl.style.width = rect.width + 'px';
                dragEl.style.zIndex = 1000;
                dragEl.style.left = rect.left + 'px';
                dragEl.style.top = rect.top + 'px';
                dragEl.classList.add('ps-dragging');
                log('[pointerSortable] pointerdown on "' + item.textContent.trim() + '"');
            }
            function onPointerMove(e) {
                if (!dragEl) return;
                dragEl.style.top = (e.clientY - offsetY) + 'px';
                const siblings = Array.from(container.querySelectorAll('[data-ps-item]')).filter(function (n) { return n !== dragEl; });
                for (const sib of siblings) {
                    const r = sib.getBoundingClientRect();
                    if (e.clientY > r.top && e.clientY < r.bottom) {
                        const after = e.clientY > r.top + r.height / 2;
                        container.insertBefore(placeholder, after ? sib.nextSibling : sib);
                        break;
                    }
                }
            }
            function onPointerUp(e) {
                if (!dragEl) return;
                dragEl.style.position = '';
                dragEl.style.width = '';
                dragEl.style.zIndex = '';
                dragEl.style.left = '';
                dragEl.style.top = '';
                dragEl.classList.remove('ps-dragging');
                placeholder.parentNode.insertBefore(dragEl, placeholder);
                placeholder.remove();
                log('[pointerSortable] dropped "' + dragEl.textContent.trim() + '"');
                dragEl = null; placeholder = null;
            }
            container.addEventListener('pointerdown', onPointerDown);
            window.addEventListener('pointermove', onPointerMove);
            window.addEventListener('pointerup', onPointerUp);
            log('pointerSortable initialized on ' + containerId);
        }
    };
})();
