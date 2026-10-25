import { AppointmentPreview, Calendar, CalendarToolbar, VIEW_MAP, ViewKey } from './calendar';
import { CancelAppointmentDialog } from './cancel-appointment';
import { RescheduleAppointmentDialog } from './reschedule-appointment';
import { ScheduleAppointmentDialog } from './schedule-appointment';
import { getGetAppointmentsQueryKey, useCancelAppointment, useGetAppointments } from '@/api/client';
import { AppointmentDto } from '@/api/client.schemas';
import { useTimeZone } from '@/components/format';
import { useModalDialog } from '@/components/modal-dialog';
import { useIsMobile } from '@/hooks/use-mobile';
import {
    type DateSelectInfo,
    type DatesSetInfo,
    type EventClickInfo,
    type EventDropInfo,
    type EventResizeDoneInfo,
    useCalendarController,
} from '@fullcalendar/react';
import { keepPreviousData, useQueryClient } from '@tanstack/react-query';
import { useCallback, useMemo, useState } from 'react';
import { toast } from 'sonner';

export const Appointments = () => {
    const modalDialog = useModalDialog();
    const queryClient = useQueryClient();
    const calendarController = useCalendarController();
    const timeZone = useTimeZone();
    const isMobile = useIsMobile();
    const initialView: ViewKey = isMobile ? 'day' : 'week';

    const [preview, setPreview] = useState<{ appointment: AppointmentDto; x: number; y: number } | null>(null);
    const [dateRange, setDateRange] = useState<{ From: string; To: string } | null>(null);

    const { mutateAsync: cancelAppointmentMutation } = useCancelAppointment();

    const { data: appointments, isFetching } = useGetAppointments(dateRange ?? { From: '', To: '' }, {
        query: { enabled: dateRange !== null, placeholderData: keepPreviousData },
    });

    const handleDatesSet = useCallback(({ start, end }: DatesSetInfo) => {
        queueMicrotask(() => setDateRange({ From: start.toISOString(), To: end.toISOString() }));
    }, []);

    const invalidateAppointments = useCallback(async () => {
        if (!dateRange) {
            return;
        }

        await queryClient.invalidateQueries({ queryKey: getGetAppointmentsQueryKey(dateRange) });
    }, [queryClient, dateRange]);

    const openScheduleDialog = useCallback(
        async (initialSlot?: { start: Date; end: Date }) => {
            const result = await modalDialog.open<boolean>(props => (
                <ScheduleAppointmentDialog {...props} initialSlot={initialSlot} />
            ));
            if (result) {
                await invalidateAppointments();
            }
        },
        [modalDialog, invalidateAppointments],
    );

    const handleSelect = async (info: DateSelectInfo) => {
        info.view.calendar.unselect();
        await openScheduleDialog({ start: info.start, end: info.end });
    };

    const handleEventClick = (info: EventClickInfo) => {
        const appointment = info.event.extendedProps as AppointmentDto;
        setPreview({ appointment, x: info.jsEvent.clientX, y: info.jsEvent.clientY });
    };

    const handleReschedule = async (appointment: AppointmentDto) => {
        setPreview(null);
        const rescheduled = await modalDialog.open<boolean>(props => (
            <RescheduleAppointmentDialog {...props} appointment={appointment} />
        ));
        if (rescheduled) {
            await invalidateAppointments();
        }
        return rescheduled;
    };

    const handleEventChange = async (info: EventDropInfo | EventResizeDoneInfo) => {
        const appointment = info.event.extendedProps as AppointmentDto;
        if (appointment.status === 'Cancelled' || !info.event.start || !info.event.end) {
            info.revert();
            return;
        }
        const rescheduled = await handleReschedule({
            ...appointment,
            start: info.event.start.toISOString(),
            end: info.event.end.toISOString(),
        });
        if (!rescheduled) {
            info.revert();
        }
    };

    const handleCancel = async (appointment: AppointmentDto) => {
        setPreview(null);
        const confirmed = await modalDialog.open<boolean>(props => <CancelAppointmentDialog {...props} />);
        if (confirmed) {
            try {
                await cancelAppointmentMutation({ id: appointment.id });
                await invalidateAppointments();
                toast.success('Appointment cancelled.');
            } catch {
                toast.error('Failed to cancel appointment.');
            }
        }
    };

    const events = useMemo(
        () =>
            appointments?.map(appointment => ({
                id: appointment.id,
                start: appointment.start,
                end: appointment.end,
                extendedProps: appointment,
            })) ?? [],
        [appointments],
    );

    return (
        <div className='flex min-h-0 w-full flex-1 flex-col'>
            <CalendarToolbar
                initialView={initialView}
                range={dateRange && { start: dateRange.From, end: dateRange.To }}
                onToday={() => calendarController.today()}
                onPrev={() => calendarController.prev()}
                onNext={() => calendarController.next()}
                onViewChange={next => calendarController.changeView(VIEW_MAP[next])}
                onNew={() => openScheduleDialog()}
            />
            <div className='min-h-0 flex-1'>
                <Calendar
                    controller={calendarController}
                    initialView={initialView}
                    timeZone={timeZone}
                    events={events}
                    loading={isFetching}
                    onSelect={handleSelect}
                    onEventClick={handleEventClick}
                    onEventChange={handleEventChange}
                    onDatesSet={handleDatesSet}
                />
            </div>
            {preview && (
                <AppointmentPreview
                    appointment={preview.appointment}
                    x={preview.x}
                    y={preview.y}
                    onClose={() => setPreview(null)}
                    onReschedule={() => handleReschedule(preview.appointment)}
                    onCancel={() => handleCancel(preview.appointment)}
                />
            )}
        </div>
    );
};
