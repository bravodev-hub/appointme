export const logout = () => {
    const form = document.createElement('form');
    form.method = 'post';
    form.action = '/api/v1/logout';
    document.body.appendChild(form);
    form.submit();
};
