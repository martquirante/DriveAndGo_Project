/**
 * CommandPalette.jsx — Global Omni-Search Command Palette (Ctrl + K / Cmd + K)
 *
 * Glassmorphic command palette triggered by keyboard shortcut.
 * Offers instant navigation actions and direct AI query dispatching.
 */

function CommandPalette({ isOpen, onClose, onOpenAiCopilot, onNavigate }) {
  const [query, setQuery] = React.useState('');
  const [selectedIndex, setSelectedIndex] = React.useState(0);
  const inputRef = React.useRef(null);

  // Focus input on open
  React.useEffect(() => {
    if (isOpen) {
      setQuery('');
      setSelectedIndex(0);
      setTimeout(() => inputRef.current?.focus(), 50);
    }
  }, [isOpen]);

  // Handle Ctrl+K / Cmd+K globally
  React.useEffect(() => {
    const handleKeyDown = (e) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        if (isOpen) onClose();
        else onNavigate?.('command_palette_open');
      }
      if (e.key === 'Escape' && isOpen) {
        onClose();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose, onNavigate]);

  if (!isOpen) return null;

  // Preset quick navigation actions
  const defaultActions = [
    { id: 'fleet', label: 'Go to Fleet Map & Status', icon: '🚘', category: 'Navigation', action: () => { onNavigate?.('fleet'); onClose(); } },
    { id: 'split', label: 'Go to Split Payments Portal', icon: '💳', category: 'Navigation', action: () => { onNavigate?.('split_pay'); onClose(); } },
    { id: 'insights', label: 'View AI Business Insights Dashboard', icon: '📊', category: 'Analytics', action: () => { onNavigate?.('insights'); onClose(); } },
    { id: 'vault', label: 'Open Document Vault & KYC', icon: '🔒', category: 'Compliance', action: () => { onNavigate?.('vault'); onClose(); } },
    { id: 'overdue', label: 'Check Overdue Rentals & Penalties', icon: '⚠️', category: 'Operations', action: () => { onOpenAiCopilot?.('Show overdue rentals'); onClose(); } },
  ];

  const filteredActions = defaultActions.filter(a => 
    a.label.toLowerCase().includes(query.toLowerCase()) ||
    a.category.toLowerCase().includes(query.toLowerCase())
  );

  const handleAskAi = () => {
    if (!query.trim()) return;
    onOpenAiCopilot?.(query.trim());
    onClose();
  };

  const handleKeyDown = (e) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      if (query.trim() && (filteredActions.length === 0 || selectedIndex === filteredActions.length)) {
        handleAskAi();
      } else if (filteredActions[selectedIndex]) {
        filteredActions[selectedIndex].action();
      }
    } else if (e.key === 'ArrowDown') {
      e.preventDefault();
      const max = query.trim() ? filteredActions.length : filteredActions.length - 1;
      setSelectedIndex(prev => (prev < max ? prev + 1 : 0));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      const max = query.trim() ? filteredActions.length : filteredActions.length - 1;
      setSelectedIndex(prev => (prev > 0 ? prev - 1 : max));
    }
  };

  return React.createElement('div', {
    style: {
      position: 'fixed', inset: 0, zIndex: 9999,
      background: 'rgba(5, 5, 12, 0.75)', backdropFilter: 'blur(12px)', WebkitBackdropFilter: 'blur(12px)',
      display: 'flex', alignItems: 'flex-start', justifyContent: 'center', paddingTop: '12vh', paddingLeft: 16, paddingRight: 16
    },
    onClick: (e) => { if (e.target === e.currentTarget) onClose(); }
  },
    React.createElement('div', {
      style: {
        width: '100%', maxWidth: 620,
        background: 'rgba(11, 12, 24, 0.95)',
        border: '1px solid rgba(255, 255, 255, 0.12)',
        borderRadius: 20,
        boxShadow: '0 25px 60px -15px rgba(0, 0, 0, 0.9), 0 0 40px rgba(234, 88, 12, 0.15)',
        overflow: 'hidden',
        display: 'flex', flexDirection: 'column',
        animation: 'fadeInUp 0.2s cubic-bezier(0.16, 1, 0.3, 1) forwards'
      }
    },
      // Input Header
      React.createElement('div', {
        style: {
          padding: '16px 20px',
          borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
          display: 'flex', alignItems: 'center', gap: 12
        }
      },
        React.createElement('svg', {
          width: 18,
          height: 18,
          viewBox: '0 0 24 24',
          fill: 'none',
          stroke: 'url(#searchIconGlow)',
          strokeWidth: 2.2,
          strokeLinecap: 'round',
          strokeLinejoin: 'round',
          style: { flexShrink: 0, opacity: 0.95 }
        },
          React.createElement('defs', null,
            React.createElement('linearGradient', { id: 'searchIconGlow', x1: '0%', y1: '0%', x2: '100%', y2: '100%' },
              React.createElement('stop', { offset: '0%', stopColor: '#f97316' }),
              React.createElement('stop', { offset: '100%', stopColor: '#a855f7' })
            )
          ),
          React.createElement('circle', { cx: 11, cy: 11, r: 7.5 }),
          React.createElement('path', { d: 'M21 21l-4.35-4.35' })
        ),
        React.createElement('input', {
          ref: inputRef,
          type: 'text',
          value: query,
          onChange: (e) => { setQuery(e.target.value); setSelectedIndex(0); },
          onKeyDown: handleKeyDown,
          placeholder: 'Type a command, search system, or ask AI… (ESC to cancel)',
          style: {
            flex: 1, background: 'transparent', border: 'none', outline: 'none',
            color: '#f8fafc', fontSize: 14, fontWeight: 500, fontFamily: 'inherit'
          }
        }),
        React.createElement('span', {
          style: {
            fontSize: 10, fontWeight: 700, color: '#94a3b8',
            background: 'rgba(255,255,255,0.06)', border: '1px solid rgba(255,255,255,0.1)',
            borderRadius: 6, padding: '2px 6px'
          }
        }, 'ESC')
      ),

      // Options List
      React.createElement('div', {
        style: {
          maxHeight: 340, overflowY: 'auto', padding: 8,
          display: 'flex', flexDirection: 'column', gap: 2
        }
      },
        // Direct "Ask AI" option if user has typed something
        query.trim() && React.createElement('button', {
          onClick: handleAskAi,
          style: {
            width: '100%', textAlign: 'left', padding: '10px 14px', borderRadius: 12,
            background: selectedIndex === filteredActions.length ? 'linear-gradient(135deg, rgba(234,88,12,0.2), rgba(139,92,246,0.2))' : 'rgba(234, 88, 12, 0.08)',
            border: selectedIndex === filteredActions.length ? '1px solid rgba(234,88,12,0.5)' : '1px solid rgba(234, 88, 12, 0.2)',
            color: '#ffffff', display: 'flex', alignItems: 'center', gap: 10, cursor: 'pointer',
            transition: 'all 0.15s'
          }
        },
          React.createElement('span', {
            style: {
              width: 28, height: 28, borderRadius: 8, background: 'linear-gradient(135deg, #ea580c, #8b5cf6)',
              display: 'flex', alignItems: 'center', justifyCenter: 'center', fontSize: 12, flexShrink: 0
            }
          }, '✨'),
          React.createElement('div', { style: { flex: 1, minWidth: 0 } },
            React.createElement('p', { style: { fontSize: 12, fontWeight: 700, color: '#fb923c', margin: 0 } }, `Ask Drive\u0026Go AI: "${query}"`),
            React.createElement('p', { style: { fontSize: 10, color: '#94a3b8', margin: 0 } }, 'Instant omniscient system query with charts & metrics')
          ),
          React.createElement('span', { style: { fontSize: 10, fontWeight: 600, color: '#ea580c' } }, '↵ Enter')
        ),

        // Quick Actions
        ...filteredActions.map((action, idx) => {
          const isSelected = idx === selectedIndex;
          return React.createElement('button', {
            key: action.id,
            onClick: action.action,
            onMouseEnter: () => setSelectedIndex(idx),
            style: {
              width: '100%', textAlign: 'left', padding: '10px 14px', borderRadius: 12,
              background: isSelected ? 'rgba(255, 255, 255, 0.08)' : 'transparent',
              border: isSelected ? '1px solid rgba(255, 255, 255, 0.12)' : '1px solid transparent',
              color: '#e2e8f0', display: 'flex', alignItems: 'center', gap: 12, cursor: 'pointer',
              transition: 'all 0.15s'
            }
          },
            React.createElement('span', { style: { fontSize: 16, flexShrink: 0 } }, action.icon),
            React.createElement('div', { style: { flex: 1, minWidth: 0 } },
              React.createElement('p', { style: { fontSize: 12, fontWeight: 600, color: isSelected ? '#ffffff' : '#e2e8f0', margin: 0 } }, action.label),
              React.createElement('p', { style: { fontSize: 9, color: '#64748b', margin: 0, textTransform: 'uppercase', letterSpacing: '0.05em' } }, action.category)
            ),
            isSelected && React.createElement('span', { style: { fontSize: 10, color: '#94a3b8' } }, '↵ Jump')
          );
        }),

        filteredActions.length === 0 && !query.trim() && React.createElement('div', {
          style: { padding: '24px 0', textAlign: 'center', color: '#64748b', fontSize: 12 }
        }, 'No matching commands.')
      ),

      // Footer Keyboard Hints
      React.createElement('div', {
        style: {
          padding: '10px 20px', background: 'rgba(5, 6, 14, 0.8)',
          borderTop: '1px solid rgba(255, 255, 255, 0.06)',
          display: 'flex', alignItems: 'center', justifyBetween: 'space-between',
          fontSize: 10, color: '#64748b'
        }
      },
        React.createElement('div', { style: { display: 'flex', gap: 12 } },
          React.createElement('span', null, '↑↓ Navigate'),
          React.createElement('span', null, '↵ Select'),
          React.createElement('span', null, 'ESC Close')
        ),
        React.createElement('div', { style: { display: 'flex', alignItems: 'center', gap: 4, color: '#ea580c', fontWeight: 600 } },
          React.createElement('span', null, '✨ DriveAndGo Omniscient AI')
        )
      )
    )
  );
}

if (typeof window !== 'undefined') {
  window.CommandPalette = CommandPalette;
}

