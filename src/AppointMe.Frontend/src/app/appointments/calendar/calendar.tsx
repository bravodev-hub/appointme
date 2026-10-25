import { AppointmentContent } from './appointment-content';
import { VIEW_MAP, ViewKey } from './calendar-config';
import { DayHeader } from './day-header';
import { AppointmentDto } from '@/api/client.schemas';
import { EventCalendarViews, Spinner } from '@/components/ui';
import { cn } from '@/lib/utils';
import type {
    CalendarController,
    DateSelectInfo,
    DatesSetInfo,
    EventClickInfo,
    EventDropInfo,
    EventInput,
    EventResizeDoneInfo,
} from '@fullcalendar/react';
import dayGridPlugin from '@fullcalendar/react/daygrid';
import interactionPlugin from '@fullcalendar/react/interaction';
import timeGridPlugin from '@fullcalendar/react/timegrid';
import { XIcon } from 'lucide-react';

const plugins = [dayGridPlugin, timeGridPlugin, interactionPlugin];

interface CalendarProps {
    controller: CalendarController;
    initialView: ViewKey;
    timeZone: string;
    events: EventInput[];
    loading?: boolean;
    onSelect: (info: DateSelectInfo) => void;
    onEventClick: (info: EventClickInfo) => void;
    onEventChange: (info: EventDropInfo | EventResizeDoneInfo) => void;
    onDatesSet: (info: DatesSetInfo) => void;
}

export const Calendar = ({
    controller,
    initialView,
    timeZone,
    events,
    loading,
    onSelect,
    onEventClick,
    onEventChange,
    onDatesSet,
}: CalendarProps) => (
    <div className='relative h-full'>
        <EventCalendarViews
            controller={controller}
            plugins={plugins}
            initialView={VIEW_MAP[initialView]}
            timeZone={timeZone}
            events={events}
            eventClick={onEventClick}
            eventContent={arg => <AppointmentContent arg={arg} />}
            dayHeaderContent={arg => <DayHeader arg={arg} />}
            datesSet={onDatesSet}
            selectable
            selectMirror
            select={onSelect}
            editable
            eventStartEditable
            eventDurationEditable
            eventResizableFromStart
            eventDrop={onEventChange}
            eventResize={onEventChange}
            eventAllow={(_, draggedEvent) => (draggedEvent?.extendedProps as AppointmentDto)?.status !== 'Cancelled'}
            height='100%'
            allDaySlot={false}
            slotMinTime='07:00:00'
            slotMaxTime='21:00:00'
            slotDuration='01:00:00'
            slotMinHeight={40}
            slotHeaderInterval='01:00:00'
            slotHeaderFormat={{ hour: '2-digit', minute: '2-digit', hour12: false }}
            nowIndicator
            dayMaxEvents={3}
            firstDay={1}
            expandRows
            popoverCloseContent={() => <XIcon className='size-4' />}
            // AppointmentContent draws the whole appointment card, so the theme's event chrome stays invisible.
            eventColor='transparent'
            eventContrastColor='var(--foreground)'
            columnEventClass=''
            columnEventInnerClass='flex h-full flex-col'
            highlightClass='bg-foreground/6'
            dayHeaderAlign='start'
            dayHeaderClass={info =>
                cn(
                    info.isDisabled && 'bg-foreground/3',
                    info.inPopover ? 'border-b bg-foreground/5' : ['border', info.isMajor && 'border-foreground/20'],
                )
            }
            dayHeaderInnerClass='flex flex-col px-3 py-2'
            dayCellClass={info =>
                cn(
                    'border',
                    info.isMajor && 'border-foreground/20',
                    (info.isDisabled || info.isToday) && 'bg-foreground/3',
                )
            }
            dayCellTopInnerClass={info =>
                cn('mx-2 my-1.5 text-xs font-medium whitespace-nowrap', info.isOther && 'text-muted-foreground')
            }
            dayLaneClass={info =>
                cn('border', info.isMajor && 'border-foreground/20', info.isDisabled && 'bg-foreground/3')
            }
            views={{
                dayGrid: {
                    listItemEventBeforeClass: 'hidden',
                    listItemEventInnerClass: 'min-w-0 flex-1',
                },
                timeGrid: {
                    slotHeaderInnerClass: 'me-2 mt-0.5 font-mono text-[10.5px] text-muted-foreground',
                    nowIndicatorHeaderClass: 'hidden',
                    nowIndicatorLineClass: 'border-t border-foreground',
                },
            }}
        />
        {loading && (
            <div className='bg-background/40 pointer-events-none absolute inset-0 z-10 flex items-center justify-center'>
                <Spinner className='size-6' />
            </div>
        )}
    </div>
);
