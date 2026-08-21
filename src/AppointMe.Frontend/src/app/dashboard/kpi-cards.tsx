import { DashboardPeriod } from './use-dashboard-period';
import { GetDashboardStatsResponse } from '@/api/appointme.schemas';
import { Card, CardContent, Progress, Skeleton } from '@/components/ui';

interface KpiCardsProps {
    stats: GetDashboardStatsResponse | undefined;
    revenue: number;
    compareRevenue: number | undefined;
    period: DashboardPeriod;
}

type DeltaKind = 'absolute' | 'percent' | 'points';

interface DeltaProps {
    current: number | null | undefined;
    compare: number | null | undefined;
    kind: DeltaKind;
    label: string;
    enabled: boolean;
}

const formatDelta = (difference: number, kind: DeltaKind): string => {
    const rounded = Math.round(Math.abs(difference) * 10) / 10;
    if (kind === 'percent') {
        return `${rounded}%`;
    }
    if (kind === 'points') {
        return `${rounded} pp`;
    }
    return rounded.toLocaleString();
};

const Delta = ({ current, compare, kind, label, enabled }: DeltaProps) => {
    if (!enabled) {
        return <span className='text-muted-foreground/70 text-xs'>—</span>;
    }
    if (current == null || compare == null || (kind === 'percent' && compare === 0)) {
        return <span className='text-muted-foreground/70 text-xs'>No comparison data</span>;
    }

    const difference = kind === 'percent' ? ((current - compare) / compare) * 100 : current - compare;
    if (Math.round(Math.abs(difference) * 10) === 0) {
        return <span className='text-muted-foreground/70 text-xs'>No change {label}</span>;
    }

    const positive = difference > 0;
    return (
        <span className='flex flex-wrap items-center gap-x-1.5 text-xs'>
            <span
                className={
                    positive
                        ? 'rounded-full bg-emerald-100 px-1.5 py-px font-medium text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300'
                        : 'rounded-full bg-red-100 px-1.5 py-px font-medium text-red-800 dark:bg-red-950 dark:text-red-300'
                }
            >
                {positive ? '▲' : '▼'} {formatDelta(difference, kind)}
            </span>
            <span className='text-muted-foreground/70'>{label}</span>
        </span>
    );
};

// Monochrome accent ramp: same foreground token at decreasing opacity.
const KPI_ACCENT_OPACITY = [1, 0.7, 0.5, 0.3];

interface KpiCardProps {
    accentIndex: number;
    label: string;
    value: string;
    sub: string;
    progress?: number | null;
    delta: DeltaProps;
}

const KpiCard = ({ accentIndex, label, value, sub, progress, delta }: KpiCardProps) => (
    <Card className='relative gap-0 overflow-hidden py-0'>
        <div
            className='bg-foreground absolute inset-x-0 top-0 h-0.5'
            style={{ opacity: KPI_ACCENT_OPACITY[accentIndex] }}
        />
        <CardContent className='flex min-h-[6.5rem] flex-1 flex-col gap-1 p-3 sm:min-h-[7.5rem] sm:p-4'>
            <div className='flex items-center gap-1.5'>
                <span
                    className='bg-foreground size-1.5 shrink-0 rounded-full'
                    style={{ opacity: KPI_ACCENT_OPACITY[accentIndex] }}
                />
                <span className='text-muted-foreground truncate text-xs font-medium'>{label}</span>
            </div>
            <div className='text-xl font-semibold tracking-tight sm:text-2xl'>{value}</div>
            <div className='text-muted-foreground/70 text-[11px] sm:text-xs'>{sub}</div>
            {progress != null && <Progress value={progress} className='mt-2 h-1' />}
            <div className='mt-auto pt-2'>
                <Delta {...delta} />
            </div>
        </CardContent>
    </Card>
);

export const KpiCards = ({ stats, revenue, compareRevenue, period }: KpiCardsProps) => {
    if (!stats) {
        return (
            <div className='grid grid-cols-2 gap-3 xl:grid-cols-4'>
                {[0, 1, 2, 3].map(index => (
                    <Skeleton key={index} className='h-[6.5rem] sm:h-[7.5rem]' />
                ))}
            </div>
        );
    }

    const enabled = period.compareEnabled;
    const label = period.compareLabel;
    const cancellations = stats.trendBuckets.reduce((sum, bucket) => sum + bucket.cancellations, 0);

    return (
        <div className='grid grid-cols-2 gap-3 xl:grid-cols-4'>
            <KpiCard
                accentIndex={0}
                label='Appointments'
                value={stats.appointments.toLocaleString()}
                sub={`${cancellations.toLocaleString()} cancellations`}
                delta={{
                    current: stats.appointments,
                    compare: stats.compareAppointments,
                    kind: 'absolute',
                    label,
                    enabled,
                }}
            />
            <KpiCard
                accentIndex={1}
                label='Revenue booked'
                value={`£${revenue.toLocaleString()}`}
                sub={
                    stats.appointments > 0
                        ? `avg £${Math.round(revenue / stats.appointments).toLocaleString()} per appointment`
                        : 'no appointments'
                }
                delta={{ current: revenue, compare: compareRevenue, kind: 'percent', label, enabled }}
            />
            <KpiCard
                accentIndex={2}
                label='Chair utilization'
                value={stats.utilizationPercent != null ? `${stats.utilizationPercent}%` : '—'}
                sub={`${Math.round(stats.bookedMinutes / 60)} of ${Math.round(stats.capacityMinutes / 60)} bookable hours`}
                progress={stats.utilizationPercent != null ? Math.min(stats.utilizationPercent, 100) : null}
                delta={{
                    current: stats.utilizationPercent,
                    compare: stats.compareUtilizationPercent,
                    kind: 'points',
                    label,
                    enabled,
                }}
            />
            <KpiCard
                accentIndex={3}
                label='Returning clients'
                value={stats.returningClientRatePercent != null ? `${stats.returningClientRatePercent}%` : '—'}
                sub={`${stats.returningAppointments} of ${stats.appointments} returning clients`}
                progress={stats.returningClientRatePercent}
                delta={{
                    current: stats.returningClientRatePercent,
                    compare: stats.compareReturningClientRatePercent,
                    kind: 'points',
                    label,
                    enabled,
                }}
            />
        </div>
    );
};
