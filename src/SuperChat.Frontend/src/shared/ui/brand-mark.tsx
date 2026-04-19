type BrandMarkProps = {
  size?: number;
};

export function BrandMark({ size = 18 }: BrandMarkProps) {
  const gradientId = `brand-mark-${size}`;
  return (
    <div className="brand-mark">
      <svg width={size} height={size} viewBox="0 0 48 48" fill="none" aria-hidden="true">
        <defs>
          <linearGradient id={gradientId} x1="0" y1="0" x2="1" y2="1">
            <stop offset="0" stopColor="#ffe49a" />
            <stop offset="0.5" stopColor="#f3c96b" />
            <stop offset="1" stopColor="#a67c1e" />
          </linearGradient>
        </defs>
        <path fill={`url(#${gradientId})`} d="M28 4 L14 26 L21 26 L19 44 L34 20 L27 20 L28 4 Z" />
      </svg>
    </div>
  );
}
