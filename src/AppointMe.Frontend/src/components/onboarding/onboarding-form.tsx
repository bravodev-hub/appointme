import { formatTimeZoneLabel } from '../format';
import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
    Spinner,
} from '../ui';
import { getGetCurrentUserQueryKey, useOnboarding } from '@/api/appointme';
import { Button } from '@/components/ui/button';
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQueryClient } from '@tanstack/react-query';
import { ComponentProps, useMemo } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useNavigate } from 'react-router';
import * as z from 'zod';

const onboardingFormSchema = z.object({
    companyName: z.string().min(1, 'Company name is required').max(250, 'Company name is too long'),
    timeZone: z.string().min(1, 'Timezone is required'),
});

type OnboardingFormValues = z.infer<typeof onboardingFormSchema>;

function getOffsetMinutes(timeZone: string, date = new Date()): number {
    const parts = new Intl.DateTimeFormat('en-US', {
        timeZone,
        hour12: false,
        timeZoneName: 'shortOffset',
    }).formatToParts(date);

    const offsetPart = parts.find(p => p.type === 'timeZoneName')?.value;

    // Examples: "GMT+1", "GMT-5", "GMT"
    if (!offsetPart || offsetPart === 'GMT') {
        return 0;
    }

    const match = offsetPart.match(/GMT([+-]\d+)/);
    if (!match) {
        return 0;
    }

    return Number(match[1]) * 60;
}
export const OnboardingForm = ({ className, ...props }: ComponentProps<'form'>) => {
    const navigate = useNavigate();
    const queryClient = useQueryClient();
    const timeZones = useMemo(
        () =>
            Intl.supportedValuesOf('timeZone')
                .map(tz => ({
                    id: tz,
                    offset: getOffsetMinutes(tz),
                }))
                .sort((a, b) => {
                    if (a.offset !== b.offset) {
                        return a.offset - b.offset;
                    }

                    return a.id.localeCompare(b.id);
                })
                .map(x => x.id),
        [],
    );
    const detectedTz = Intl.DateTimeFormat().resolvedOptions().timeZone;

    const { control, formState, handleSubmit, setError } = useForm<OnboardingFormValues>({
        resolver: zodResolver(onboardingFormSchema),
        defaultValues: {
            companyName: '',
            timeZone: detectedTz,
        },
    });
    const { mutateAsync: onboarding } = useOnboarding();

    const onSubmit = async (data: OnboardingFormValues) => {
        try {
            await onboarding({ data });
            await queryClient.invalidateQueries({ queryKey: getGetCurrentUserQueryKey() });
            navigate('/');
        } catch {
            setError('root', {
                type: 'server',
                message: 'Something went wrong. Please try again.',
            });
        }
    };

    return (
        <Card>
            <CardHeader className='text-center'>
                <CardTitle className='text-xl'>Complete your account</CardTitle>
                <CardDescription>Enter your company details to complete your account</CardDescription>
            </CardHeader>
            <CardContent>
                <form className={cn('flex flex-col gap-6', className)} onSubmit={handleSubmit(onSubmit)} {...props}>
                    <FieldGroup>
                        <Controller
                            name='companyName'
                            control={control}
                            render={({ field, fieldState }) => (
                                <Field data-invalid={fieldState.invalid}>
                                    <FieldLabel htmlFor='companyName'>Company name</FieldLabel>
                                    <Input
                                        {...field}
                                        id='companyName'
                                        type='text'
                                        placeholder='Acme Inc.'
                                        autoComplete='off'
                                        aria-invalid={fieldState.invalid}
                                        aria-describedby={fieldState.invalid ? 'companyName-error' : undefined}
                                    />
                                    {fieldState.invalid && (
                                        <FieldError id='companyName-error' errors={[fieldState.error]} />
                                    )}
                                </Field>
                            )}
                        />
                        <Controller
                            name='timeZone'
                            control={control}
                            render={({ field, fieldState }) => (
                                <Field data-invalid={fieldState.invalid}>
                                    <FieldLabel htmlFor='timeZone'>Timezone</FieldLabel>
                                    <Select name='timeZone' value={field.value} onValueChange={field.onChange}>
                                        <SelectTrigger id='timeZone'>
                                            <SelectValue placeholder='Select timezone' />
                                        </SelectTrigger>
                                        <SelectContent className='max-h-75'>
                                            {timeZones.map(timeZone => (
                                                <SelectItem key={timeZone} value={timeZone}>
                                                    {formatTimeZoneLabel(timeZone)}
                                                    {timeZone === detectedTz && (
                                                        <span className='text-muted-foreground ml-2 text-xs'>
                                                            (detected)
                                                        </span>
                                                    )}
                                                </SelectItem>
                                            ))}
                                        </SelectContent>
                                    </Select>

                                    <FieldDescription>
                                        Used for appointment scheduling and notifications
                                    </FieldDescription>

                                    {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
                                </Field>
                            )}
                        />
                        {formState.errors.root && <FieldError errors={[formState.errors.root]} />}
                        <Field>
                            <Button type='submit' disabled={formState.isSubmitting}>
                                {formState.isSubmitting ? (
                                    <>
                                        <Spinner />
                                        Registering company...
                                    </>
                                ) : (
                                    'Complete Account'
                                )}
                            </Button>
                        </Field>
                    </FieldGroup>
                </form>
            </CardContent>
        </Card>
    );
};
