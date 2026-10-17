import { APP_VERSION } from './version';
import { FormattedDate } from '@/components/format';
import { useState } from 'react';

export const Footer = () => {
    const [now] = useState(() => new Date());

    return (
        <footer className='p-4 text-center text-xs text-gray-500'>
            © <FormattedDate date={now} format='year' />{' '}
            <span className='font-extralight'>
                Appoint<span className='font-semibold'>Me</span>
            </span>
            <span className='text-gray-400'> · {APP_VERSION}</span>
        </footer>
    );
};
