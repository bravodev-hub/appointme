import { useTimeZone } from '@/components/format';
import { parseAsStringLiteral, useQueryState } from 'nuqs';
import { Temporal } from 'temporal-polyfill';

export const PERIOD_PRESETS = ['today', 'yesterday', 'week', 'lastWeek', 'month', 'quarter', 'year'] as const;
export type PeriodPreset = (typeof PERIOD_PRESETS)[number];

export const COMPARE_MODES = ['previous', 'none'] as const;
export type CompareMode = (typeof COMPARE_MODES)[number];

export type TrendBucket = 'day' | 'week' | 'month';

export const PERIOD_LABELS: Record<PeriodPreset, string> = {
    today: 'Today',
    yesterday: 'Yesterday',
    week: 'This week',
    lastWeek: 'Last week',
    month: 'This month',
    quarter: 'This quarter',
    year: 'This year',
};

const COMPARE_LABELS: Record<PeriodPreset, string> = {
    today: 'vs. yesterday',
    yesterday: 'vs. day before',
    week: 'vs. last week',
    lastWeek: 'vs. prior week',
    month: 'vs. last month',
    quarter: 'vs. last quarter',
    year: 'vs. last year',
};

const BUCKETS: Record<PeriodPreset, TrendBucket> = {
    today: 'day',
    yesterday: 'day',
    week: 'day',
    lastWeek: 'day',
    month: 'day',
    quarter: 'week',
    year: 'month',
};

// Every preset is a calendar range: [start, end) as local midnights in the
// company time zone. Week starts Monday (ISO).
const computeRange = (
    preset: PeriodPreset,
    timeZone: string,
): { start: Temporal.PlainDate; end: Temporal.PlainDate } => {
    const today = Temporal.Now.plainDateISO(timeZone);
    const monday = today.subtract({ days: today.dayOfWeek - 1 });
    switch (preset) {
        case 'today':
            return { start: today, end: today.add({ days: 1 }) };
        case 'yesterday':
            return { start: today.subtract({ days: 1 }), end: today };
        case 'week':
            return { start: monday, end: monday.add({ weeks: 1 }) };
        case 'lastWeek':
            return { start: monday.subtract({ weeks: 1 }), end: monday };
        case 'month': {
            const first = today.with({ day: 1 });
            return { start: first, end: first.add({ months: 1 }) };
        }
        case 'quarter': {
            const first = today.with({ month: today.month - ((today.month - 1) % 3), day: 1 });
            return { start: first, end: first.add({ months: 3 }) };
        }
        case 'year': {
            const first = today.with({ month: 1, day: 1 });
            return { start: first, end: first.add({ years: 1 }) };
        }
    }
};

// The comparison period is the same preset shifted one calendar unit back
// (previous calendar week/month/quarter/year), so labels match user intuition.
// Bucket counts can differ (e.g. 30- vs 31-day months); consumers pair by index.
const COMPARE_SHIFTS: Record<PeriodPreset, Temporal.DurationLike> = {
    today: { days: 1 },
    yesterday: { days: 1 },
    week: { weeks: 1 },
    lastWeek: { weeks: 1 },
    month: { months: 1 },
    quarter: { months: 3 },
    year: { years: 1 },
};

const toUtcIso = (date: Temporal.PlainDate, timeZone: string): string =>
    date.toZonedDateTime(timeZone).toInstant().toString();

const formatRangeLabel = (start: Temporal.PlainDate, end: Temporal.PlainDate): string => {
    const dayMonth = (date: Temporal.PlainDate) => date.toLocaleString(undefined, { day: 'numeric', month: 'short' });
    const lastDay = end.subtract({ days: 1 });
    if (lastDay.equals(start)) {
        return dayMonth(start);
    }
    return `${dayMonth(start)} – ${dayMonth(lastDay)}`;
};

export interface DashboardPeriod {
    preset: PeriodPreset;
    setPreset: (preset: PeriodPreset) => void;
    compare: CompareMode;
    setCompare: (compare: CompareMode) => void;
    compareEnabled: boolean;
    timeZone: string;
    from: string;
    to: string;
    compareFrom?: string;
    compareTo?: string;
    bucket: TrendBucket;
    rangeLabel: string;
    compareLabel: string;
}

export const useDashboardPeriod = (): DashboardPeriod => {
    const timeZone = useTimeZone();
    const [preset, setPreset] = useQueryState('period', parseAsStringLiteral(PERIOD_PRESETS).withDefault('week'));
    const [compare, setCompare] = useQueryState('compare', parseAsStringLiteral(COMPARE_MODES).withDefault('previous'));

    const { start, end } = computeRange(preset, timeZone);
    const compareEnabled = compare === 'previous';
    const shift = COMPARE_SHIFTS[preset];

    return {
        preset,
        setPreset,
        compare,
        setCompare,
        compareEnabled,
        timeZone,
        from: toUtcIso(start, timeZone),
        to: toUtcIso(end, timeZone),
        compareFrom: compareEnabled ? toUtcIso(start.subtract(shift), timeZone) : undefined,
        compareTo: compareEnabled ? toUtcIso(end.subtract(shift), timeZone) : undefined,
        bucket: BUCKETS[preset],
        rangeLabel: formatRangeLabel(start, end),
        compareLabel: COMPARE_LABELS[preset],
    };
};
