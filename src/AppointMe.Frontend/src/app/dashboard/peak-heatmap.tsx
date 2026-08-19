import { useGetPeakHours } from '@/api/appointme';
import { useTimeZone } from '@/components/format';
import {
    Card,
    CardContent,
    CardHeader,
    CardTitle,
    Empty,
    EmptyDescription,
    EmptyHeader,
    EmptyTitle,
    Skeleton,
} from '@/components/ui';
import { useMemo } from 'react';
import { Temporal } from 'temporal-polyfill';

const DAY_LABELS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
// The grid shows classic salon opening hours; data outside this window exists in
// the response but is not rendered.
const DISPLAY_HOURS = Array.from({ length: 12 }, (_, index) => index + 8);

const formatCellValue = (value: number): string => {
    if (value <= 0) {
        return '';
    }
    return value >= 10 ? String(Math.round(value)) : value.toFixed(1);
};

export const PeakHeatmap = () => {
    const timeZone = useTimeZone();

    // Last 28 full days ending today (inclusive), in the company time zone.
    const range = useMemo(() => {
        const today = Temporal.Now.plainDateISO(timeZone);
        const toUtcIso = (date: Temporal.PlainDate) => date.toZonedDateTime(timeZone).toInstant().toString();
        return {
            From: toUtcIso(today.subtract({ days: 27 })),
            To: toUtcIso(today.add({ days: 1 })),
        };
    }, [timeZone]);

    const { data } = useGetPeakHours(
        { From: range.From, To: range.To, TimeZone: timeZone },
        { query: { staleTime: 5 * 60 * 1000 } },
    );

    if (!data) {
        return <Skeleton className='h-[300px]' />;
    }

    const hasData = data.days.some(day => DISPLAY_HOURS.some(hour => (day.hourlyAverages[hour] ?? 0) > 0));
    const max = Math.max(...data.days.flatMap(day => DISPLAY_HOURS.map(hour => day.hourlyAverages[hour] ?? 0)), 0.1);

    const swatchStyle = (intensity: number) =>
        intensity > 0
            ? { background: `color-mix(in srgb, var(--foreground) ${Math.round(intensity * 85)}%, transparent)` }
            : undefined;

    return (
        <Card>
            <CardHeader className='flex flex-wrap items-baseline justify-between gap-2'>
                <div>
                    <CardTitle className='text-sm'>Peak hours</CardTitle>
                    <p className='text-muted-foreground mt-1 text-xs'>Average bookings per hour, last 4 weeks</p>
                </div>
                <div className='text-muted-foreground/70 flex items-center gap-1.5 text-[11px]'>
                    <span>Less</span>
                    <div className='flex gap-0.5'>
                        {[0, 0.25, 0.5, 0.75, 1].map(intensity => (
                            <div
                                key={intensity}
                                className={`size-3.5 rounded-xs ${intensity === 0 ? 'bg-muted border' : ''}`}
                                style={swatchStyle(intensity)}
                            />
                        ))}
                    </div>
                    <span>More</span>
                </div>
            </CardHeader>
            <CardContent>
                {!hasData && (
                    <Empty className='h-[240px]'>
                        <EmptyHeader>
                            <EmptyTitle>No bookings yet</EmptyTitle>
                            <EmptyDescription>
                                Peak hours appear once appointments land in the last 4 weeks.
                            </EmptyDescription>
                        </EmptyHeader>
                    </Empty>
                )}
                {hasData && (
                    <div className='flex justify-center overflow-x-auto'>
                        <div
                            className='grid min-w-fit flex-1 gap-0.75'
                            // Cells grow to fill the card, capped so they stay recognisably square-ish;
                            // min-width keeps them legible and lets the container scroll on narrow screens.
                            style={{
                                gridTemplateColumns: `2.5rem repeat(${DISPLAY_HOURS.length}, minmax(1.75rem, 2.5rem))`,
                            }}
                        >
                            <div />
                            {DISPLAY_HOURS.map(hour => (
                                <div key={hour} className='text-muted-foreground/70 text-center font-mono text-[10px]'>
                                    {hour}
                                </div>
                            ))}
                            {data.days.map(day => (
                                <div key={day.isoWeekday} className='contents'>
                                    <div className='text-muted-foreground flex items-center text-[11px] font-medium'>
                                        {DAY_LABELS[day.isoWeekday - 1]}
                                    </div>
                                    {DISPLAY_HOURS.map(hour => {
                                        const value = day.hourlyAverages[hour] ?? 0;
                                        const intensity = value / max;
                                        return (
                                            <div
                                                key={hour}
                                                title={`${DAY_LABELS[day.isoWeekday - 1]} ${hour}:00 — ${value} avg`}
                                                className={`flex h-7 w-full items-center justify-center rounded-sm font-mono text-[10px] font-medium ${
                                                    value === 0 ? 'bg-muted/50 border' : ''
                                                } ${intensity > 0.55 ? 'text-background' : 'text-foreground'}`}
                                                style={swatchStyle(intensity)}
                                            >
                                                {formatCellValue(value)}
                                            </div>
                                        );
                                    })}
                                </div>
                            ))}
                        </div>
                    </div>
                )}
            </CardContent>
        </Card>
    );
};
