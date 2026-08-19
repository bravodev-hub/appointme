import { revenueForBucket } from './dummy-revenue';
import { DashboardPeriod, PERIOD_LABELS } from './use-dashboard-period';
import { useGetNewCustomerStats } from '@/api/appointme';
import { GetDashboardStatsResponse, TrendBucketDto } from '@/api/appointme.schemas';
import { useCurrentCompany, usePermission } from '@/components/auth';
import {
    Card,
    CardContent,
    CardHeader,
    CardTitle,
    ChartContainer,
    ChartTooltip,
    ChartTooltipContent,
    Empty,
    EmptyDescription,
    EmptyHeader,
    EmptyTitle,
    Skeleton,
    ToggleGroup,
    ToggleGroupItem,
} from '@/components/ui';
import { keepPreviousData } from '@tanstack/react-query';
import { useState } from 'react';
import { Bar, BarChart, CartesianGrid, XAxis, YAxis } from 'recharts';
import { Temporal } from 'temporal-polyfill';

type SeriesKey = 'appointments' | 'revenue' | 'cancellations' | 'newCustomers';

const SERIES_LABELS: Record<SeriesKey, string> = {
    appointments: 'Appointments',
    revenue: 'Revenue',
    cancellations: 'Cancellations',
    newCustomers: 'New customers',
};

interface TrendChartProps {
    stats: GetDashboardStatsResponse | undefined;
    period: DashboardPeriod;
}

// bucketStart is a plain calendar date from the backend (DateOnly, "2026-08-18").
const bucketLabel = (bucketStart: string, bucket: DashboardPeriod['bucket']): string => {
    const date = Temporal.PlainDate.from(bucketStart);
    if (bucket === 'day') {
        return date.toLocaleString(undefined, { weekday: 'short', day: 'numeric' });
    }
    if (bucket === 'week') {
        return date.toLocaleString(undefined, { day: 'numeric', month: 'short' });
    }
    return date.toLocaleString(undefined, { month: 'short' });
};

const formatValue = (value: number, series: SeriesKey): string =>
    series === 'revenue' ? `£${value.toLocaleString()}` : value.toLocaleString();

export const TrendChart = ({ stats, period }: TrendChartProps) => {
    const { currentCompany } = useCurrentCompany();
    const canViewAppointmentStats = usePermission('appointments.statistics:view');
    const canViewCustomerStats = usePermission('customers.statistics:view');
    const [series, setSeries] = useState<SeriesKey>(canViewAppointmentStats ? 'appointments' : 'newCustomers');

    const { data: newCustomers } = useGetNewCustomerStats(
        {
            From: period.from,
            To: period.to,
            CompareFrom: period.compareFrom,
            CompareTo: period.compareTo,
            Bucket: period.bucket,
            TimeZone: period.timeZone,
        },
        { query: { enabled: canViewCustomerStats, placeholderData: keepPreviousData } },
    );

    if (canViewAppointmentStats && !stats) {
        return <Skeleton className='h-[340px]' />;
    }

    if (series === 'newCustomers' && canViewCustomerStats && !newCustomers) {
        return <Skeleton className='h-[340px]' />;
    }

    const valueFor = (bucket: TrendBucketDto): number | null => {
        switch (series) {
            case 'appointments':
                return bucket.appointments;
            case 'cancellations':
                return bucket.cancellations;
            case 'revenue':
                return revenueForBucket(currentCompany.companyId, bucket);
            case 'newCustomers':
                return null;
        }
    };

    // The newCustomers series charts the customer stats response on its own, so
    // it works without appointment stats (and without their permission).
    const data =
        series === 'newCustomers'
            ? (newCustomers?.buckets ?? []).map((bucket, index) => ({
                  label: bucketLabel(bucket.bucketStart, period.bucket),
                  current: bucket.count as number | null,
                  compare: period.compareEnabled ? (newCustomers?.compareBuckets[index]?.count ?? null) : null,
              }))
            : (stats?.trendBuckets ?? []).map((bucket, index) => {
                  const compareBucket = stats?.compareTrendBuckets[index];
                  return {
                      label: bucketLabel(bucket.bucketStart, period.bucket),
                      current: valueFor(bucket),
                      compare: compareBucket ? valueFor(compareBucket) : null,
                  };
              });

    const total = data.reduce((sum, entry) => sum + (entry.current ?? 0), 0);
    const compareTotal = data.reduce((sum, entry) => sum + (entry.compare ?? 0), 0);
    const deltaPercent = compareTotal > 0 ? Math.round(((total - compareTotal) / compareTotal) * 1000) / 10 : null;

    const chartConfig = {
        current: { label: PERIOD_LABELS[period.preset], color: 'var(--foreground)' },
        compare: { label: period.compareLabel.replace('vs. ', ''), color: 'var(--muted-foreground)' },
    };

    return (
        <Card>
            <CardHeader className='flex flex-wrap items-start justify-between gap-3'>
                <div>
                    <CardTitle className='text-sm'>
                        {SERIES_LABELS[series]} · {PERIOD_LABELS[period.preset].toLowerCase()}
                    </CardTitle>
                    <p className='text-muted-foreground mt-1 text-xs'>
                        {formatValue(total, series)}
                        {period.compareEnabled && deltaPercent != null && (
                            <>
                                {' '}
                                ({deltaPercent > 0 ? '+' : ''}
                                {deltaPercent}% {period.compareLabel})
                            </>
                        )}
                    </p>
                </div>
                <ToggleGroup
                    type='single'
                    variant='outline'
                    size='sm'
                    value={series}
                    onValueChange={value => value && setSeries(value as SeriesKey)}
                >
                    {canViewAppointmentStats && (
                        <>
                            <ToggleGroupItem value='appointments'>Appointments</ToggleGroupItem>
                            <ToggleGroupItem value='revenue'>Revenue</ToggleGroupItem>
                            <ToggleGroupItem value='cancellations'>Cancellations</ToggleGroupItem>
                        </>
                    )}
                    {canViewCustomerStats && <ToggleGroupItem value='newCustomers'>New customers</ToggleGroupItem>}
                </ToggleGroup>
            </CardHeader>
            <CardContent>
                {data.every(entry => !entry.current && !entry.compare) ? (
                    <Empty className='h-[240px]'>
                        <EmptyHeader>
                            <EmptyTitle>Nothing to chart</EmptyTitle>
                            <EmptyDescription>
                                No {SERIES_LABELS[series].toLowerCase()} in this period.
                            </EmptyDescription>
                        </EmptyHeader>
                    </Empty>
                ) : (
                    <ChartContainer config={chartConfig} className='aspect-auto h-[240px] w-full'>
                        <BarChart data={data} barGap={2}>
                            <CartesianGrid vertical={false} strokeDasharray='3 3' />
                            <XAxis dataKey='label' tickLine={false} axisLine={false} fontSize={11} />
                            <YAxis width={40} tickLine={false} axisLine={false} fontSize={11} />
                            <ChartTooltip content={<ChartTooltipContent />} />
                            {period.compareEnabled && (
                                <Bar
                                    dataKey='compare'
                                    fill='var(--color-compare)'
                                    fillOpacity={0.45}
                                    radius={[3, 3, 0, 0]}
                                />
                            )}
                            <Bar dataKey='current' fill='var(--color-current)' radius={[3, 3, 0, 0]} />
                        </BarChart>
                    </ChartContainer>
                )}
            </CardContent>
        </Card>
    );
};
