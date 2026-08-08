import { APP_VERSION } from './version';
import { FormattedDate } from '@/components/format';

export const Footer = () => {
    return (
        <footer className='p-4 text-center text-xs text-gray-500'>
            © <FormattedDate date={new Date()} format='year' />{' '}
            <span className='font-extralight'>
                Appoint<span className='font-semibold'>Me</span>
            </span>
            <span className='text-gray-400'> · {APP_VERSION}</span>
        </footer>
    );
};
