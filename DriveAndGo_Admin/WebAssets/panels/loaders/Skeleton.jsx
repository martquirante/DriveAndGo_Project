/**
 * Skeleton Component — High Performance React Skeleton Loader with Shimmer Effect
 * Supports dark theme styling (#161824 base to #292d3f shimmer).
 */
(function() {
  if (typeof document !== 'undefined' && !document.getElementById('skeleton-shimmer-styles')) {
    const style = document.createElement('style');
    style.id = 'skeleton-shimmer-styles';
    style.innerHTML = `
      @keyframes skeletonShimmer {
        0% {
          background-position: -200% 0;
        }
        100% {
          background-position: 200% 0;
        }
      }

      .skeleton-shimmer-base {
        background: linear-gradient(90deg, #161824 0%, #292d3f 50%, #161824 100%);
        background-size: 200% 100%;
        animation: skeletonShimmer 1.8s infinite linear;
        border-radius: 8px;
        display: inline-block;
      }

      .skeleton-avatar {
        border-radius: 50% !important;
      }
    `;
    document.head.appendChild(style);
  }
})();

function Skeleton({ width = '100%', height = '20px', borderRadius = '8px', className = '', style = {} }) {
  const customStyle = {
    width: width,
    height: height,
    borderRadius: borderRadius,
    ...style
  };

  return (
    <div
      className={`skeleton-shimmer-base ${className}`}
      style={customStyle}
    />
  );
}

function SkeletonRow({ columns = 4, className = '' }) {
  return (
    <div className={`flex items-center gap-3 p-3 w-full bg-[#0d0f19]/40 rounded-xl border border-white/5 ${className}`}>
      <Skeleton width="40px" height="40px" borderRadius="50%" className="skeleton-avatar shrink-0" />
      <div className="flex-1 space-y-2">
        <Skeleton width="60%" height="16px" />
        <Skeleton width="40%" height="12px" />
      </div>
      <Skeleton width="80px" height="24px" borderRadius="12px" className="shrink-0" />
    </div>
  );
}

function SkeletonCard({ className = '' }) {
  return (
    <div className={`p-5 w-full bg-[#111322] rounded-2xl border border-white/10 space-y-4 ${className}`}>
      <div className="flex items-center justify-between">
        <Skeleton width="44px" height="44px" borderRadius="50%" />
        <Skeleton width="60px" height="20px" borderRadius="10px" />
      </div>
      <Skeleton width="70%" height="24px" />
      <Skeleton width="40%" height="14px" />
      <Skeleton width="100%" height="8px" borderRadius="4px" />
    </div>
  );
}

function SkeletonChatMessage({ isUser = false }) {
  return (
    <div className={`flex gap-3 my-3 w-full ${isUser ? 'justify-end' : 'justify-start'}`}>
      {!isUser && <Skeleton width="36px" height="36px" borderRadius="50%" className="shrink-0" />}
      <div className={`space-y-2 max-w-[70%] ${isUser ? 'items-end' : 'items-start'}`}>
        <Skeleton width="180px" height="14px" />
        <Skeleton width="240px" height="40px" borderRadius="16px" />
      </div>
      {isUser && <Skeleton width="36px" height="36px" borderRadius="50%" className="shrink-0" />}
    </div>
  );
}

if (typeof window !== 'undefined') {
  window.Skeleton = Skeleton;
  window.SkeletonRow = SkeletonRow;
  window.SkeletonCard = SkeletonCard;
  window.SkeletonChatMessage = SkeletonChatMessage;
}
