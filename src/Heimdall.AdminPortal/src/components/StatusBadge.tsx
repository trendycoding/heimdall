interface StatusBadgeProps {
  status: string;
  variant?: 'default' | 'small';
}

const statusColors: Record<string, string> = {
  active: 'bg-green-100 text-green-800',
  inactive: 'bg-gray-100 text-gray-800',
  suspended: 'bg-yellow-100 text-yellow-800',
  allow: 'bg-green-100 text-green-800',
  deny: 'bg-red-100 text-red-800',
};

/**
 * Color-coded status indicator badge.
 */
export function StatusBadge({ status, variant = 'default' }: StatusBadgeProps) {
  const colorClasses = statusColors[status.toLowerCase()] ?? 'bg-gray-100 text-gray-800';
  const sizeClasses = variant === 'small' ? 'px-1.5 py-0.5 text-xs' : 'px-2.5 py-0.5 text-xs';

  return (
    <span
      className={`inline-flex items-center rounded-full font-medium ${colorClasses} ${sizeClasses}`}
    >
      {status}
    </span>
  );
}
