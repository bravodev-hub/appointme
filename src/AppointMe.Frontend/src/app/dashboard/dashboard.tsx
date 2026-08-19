import { totalRevenue } from './dummy-revenue';
import { KpiCards } from './kpi-cards';
import { PeakHeatmap } from './peak-heatmap';
import { PeriodPicker } from './period-picker';
import { StaffLoad } from './staff-load';
import { TrendChart } from './trend-chart';
import { useDashboardPeriod } from './use-dashboard-period';
import { useGetDashboardStats } from '@/api/appointme';
import { Can, useCurrentCompany, usePermission } from '@/components/auth';
import { ErrorWidget } from '@/components/error/error-widget';
import { FormattedDate } from '@/components/format';
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui';
import { keepPreviousData } from '@tanstack/react-query';
import { ChartPieIcon } from 'lucide-react';

export const Dashboard = () => (
    <Can permission={['appointments.statistics:view', 'customers.statistics:view']} fallback={<NoAccess />}>
        <DashboardContent />
    </Can>
);

const NoAccess = () => (
    <Empty>
        <EmptyHeader>
            <EmptyMedia variant='icon'>
                <ChartPieIcon />
            </EmptyMedia>
            <EmptyTitle>Dashboard access is limited</EmptyTitle>
            <EmptyDescription>
                Business stats are visible to company owners and teammates they've shared access with.
            </EmptyDescription>
        </EmptyHeader>
    </Empty>
);

const DashboardContent = () => {
    const period = useDashboardPeriod();
    const { currentCompany } = useCurrentCompany();
    const canViewAppointmentStats = usePermission('appointments.statistics:view');

    const {
        data: stats,
        isError,
        refetch,
    } = useGetDashboardStats(
        {
            From: period.from,
            To: period.to,
            CompareFrom: period.compareFrom,
            CompareTo: period.compareTo,
            Bucket: period.bucket,
            TimeZone: period.timeZone,
        },
        { query: { enabled: canViewAppointmentStats, placeholderData: keepPreviousData } },
    );

    if (isError) {
        return (
            <div className='flex min-h-[50vh] items-center justify-center p-6'>
                <ErrorWidget title='Failed to load dashboard' onRetry={() => refetch()} />
            </div>
        );
    }

    const revenue = stats ? totalRevenue(currentCompany.companyId, stats.trendBuckets) : 0;
    const compareRevenue =
        stats && period.compareEnabled ? totalRevenue(currentCompany.companyId, stats.compareTrendBuckets) : undefined;

    return (
        <div className='mx-auto flex w-full max-w-7xl flex-col gap-4'>
            <div className='flex flex-wrap items-end justify-between gap-3'>
                <div>
                    <h1 className='text-xl font-semibold tracking-tight'>Dashboard</h1>
                    <p className='text-muted-foreground text-sm'>
                        <FormattedDate date={new Date()} />
                    </p>
                </div>
                <PeriodPicker period={period} />
            </div>

            {canViewAppointmentStats && (
                <KpiCards stats={stats} revenue={revenue} compareRevenue={compareRevenue} period={period} />
            )}
            <TrendChart stats={stats} period={period} />
            {canViewAppointmentStats && (
                <div className='grid gap-4 xl:grid-cols-2'>
                    <StaffLoad staffLoad={stats?.staffLoad} preset={period.preset} />
                    <PeakHeatmap />
                </div>
            )}
        </div>
    );
};
