import { CompareMode, DashboardPeriod, PERIOD_LABELS, PERIOD_PRESETS, PeriodPreset } from './use-dashboard-period';
import {
    Button,
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuLabel,
    DropdownMenuRadioGroup,
    DropdownMenuRadioItem,
    DropdownMenuSeparator,
    DropdownMenuTrigger,
} from '@/components/ui';
import { CalendarIcon, ChevronDownIcon } from 'lucide-react';

interface PeriodPickerProps {
    period: DashboardPeriod;
}

export const PeriodPicker = ({ period }: PeriodPickerProps) => (
    <DropdownMenu>
        <DropdownMenuTrigger asChild>
            <Button variant='outline' size='sm'>
                <CalendarIcon />
                {PERIOD_LABELS[period.preset]}
                <span className='text-muted-foreground hidden font-mono text-xs font-normal sm:inline'>
                    {period.rangeLabel}
                </span>
                <ChevronDownIcon className='text-muted-foreground' />
            </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align='end' className='w-56'>
            <DropdownMenuLabel className='text-muted-foreground text-xs'>Period</DropdownMenuLabel>
            <DropdownMenuRadioGroup
                value={period.preset}
                onValueChange={value => period.setPreset(value as PeriodPreset)}
            >
                {PERIOD_PRESETS.map(preset => (
                    <DropdownMenuRadioItem key={preset} value={preset}>
                        {PERIOD_LABELS[preset]}
                    </DropdownMenuRadioItem>
                ))}
            </DropdownMenuRadioGroup>
            <DropdownMenuSeparator />
            <DropdownMenuLabel className='text-muted-foreground text-xs'>Compare to</DropdownMenuLabel>
            <DropdownMenuRadioGroup
                value={period.compare}
                onValueChange={value => period.setCompare(value as CompareMode)}
            >
                <DropdownMenuRadioItem value='previous'>Previous period</DropdownMenuRadioItem>
                <DropdownMenuRadioItem value='none'>No comparison</DropdownMenuRadioItem>
            </DropdownMenuRadioGroup>
        </DropdownMenuContent>
    </DropdownMenu>
);
