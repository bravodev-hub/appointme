import { AppointmentContent } from './appointment-content';
import { VIEW_MAP, ViewKey } from './calendar-config';
import { DayHeader } from './day-header';
import './fullcalendar.css';
import { AppointmentDto } from '@/api/client.schemas';
import { Spinner } from '@/components/ui';
import type { DateSelectArg, DatesSetArg, EventClickArg, EventDropArg, EventInput } from '@fullcalendar/core';
import dayGridPlugin from '@fullcalendar/daygrid';
import type { EventResizeDoneArg } from '@fullcalendar/interaction';
import interactionPlugin from '@fullcalendar/interaction';
import luxon3Plugin from '@fullcalendar/luxon3';
import FullCalendar from '@fullcalendar/react';
import timeGridPlugin from '@fullcalendar/timegrid';
import { forwardRef, useImperativeHandle, useRef } from 'react';

interface CalendarProps {
    initialView: ViewKey;
    timeZone: string;
    events: EventInput[];
    loading?: boolean;
    onSelect: (info: DateSelectArg) => void;
    onEventClick: (info: EventClickArg) => void;
    onEventChange: (info: EventDropArg | EventResizeDoneArg) => void;
    onDatesSet: (arg: DatesSetArg) => void;
}

export const Calendar = forwardRef<FullCalendar, CalendarProps>(
    ({ initialView, timeZone, events, loading, onSelect, onEventClick, onEventChange, onDatesSet }, ref) => {
        const innerRef = useRef<FullCalendar>(null);
        useImperativeHandle(ref, () => innerRef.current as FullCalendar, []);

        return (
            <div className='relative h-full'>
                <FullCalendar
                    ref={innerRef}
                    plugins={[dayGridPlugin, timeGridPlugin, interactionPlugin, luxon3Plugin]}
                    initialView={VIEW_MAP[initialView]}
                    timeZone={timeZone}
                    headerToolbar={false}
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
                    eventAllow={(_, draggedEvent) =>
                        (draggedEvent?.extendedProps as AppointmentDto)?.status !== 'Cancelled'
                    }
                    height='100%'
                    allDaySlot={false}
                    slotMinTime='07:00:00'
                    slotMaxTime='21:00:00'
                    slotDuration='01:00:00'
                    slotLabelInterval='01:00:00'
                    slotLabelFormat={{ hour: '2-digit', minute: '2-digit', hour12: false }}
                    nowIndicator
                    dayMaxEvents={3}
                    firstDay={1}
                    expandRows
                />
                {loading && (
                    <div className='bg-background/40 pointer-events-none absolute inset-0 z-10 flex items-center justify-center'>
                        <Spinner className='size-6' />
                    </div>
                )}
            </div>
        );
    },
);

Calendar.displayName = 'Calendar';
