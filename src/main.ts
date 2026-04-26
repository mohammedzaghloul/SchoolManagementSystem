import { platformBrowserDynamic } from '@angular/platform-browser-dynamic';
import { AppModule } from './app/app.module';

const ensureAccessibleFormFields = () => {
    let generatedFieldId = 0;

    const fieldSelector = 'input, select, textarea';
    const getReadableLabel = (field: HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement): string => {
        const explicitLabel = field.id ? document.querySelector(`label[for="${CSS.escape(field.id)}"]`) : null;
        const nearbyLabel = field.closest('.form-group, .filter-group, .filter-card, .form-field, .input-wrapper, .payment-input, .grade-input, .notes-field')
            ?.querySelector('label');
        const rawLabel = explicitLabel?.textContent
            || nearbyLabel?.textContent
            || field.getAttribute('placeholder')
            || field.getAttribute('formcontrolname')
            || field.getAttribute('ng-reflect-name')
            || field.getAttribute('name')
            || field.getAttribute('type')
            || 'form field';

        return rawLabel.replace(/\s+/g, ' ').trim();
    };

    const normalizeToken = (value: string) => value
        .toLowerCase()
        .replace(/[^a-z0-9\u0600-\u06FF]+/g, '-')
        .replace(/^-+|-+$/g, '')
        || 'field';

    const patchField = (field: HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement) => {
        if (field.type === 'hidden') {
            return;
        }

        const readableLabel = getReadableLabel(field);
        const baseName = normalizeToken(
            field.getAttribute('name')
            || field.getAttribute('formcontrolname')
            || field.getAttribute('ng-reflect-name')
            || readableLabel
        );

        if (!field.id) {
            generatedFieldId += 1;
            field.id = `${baseName}-${generatedFieldId}`;
        }

        if (!field.getAttribute('name')) {
            field.setAttribute('name', baseName);
        }

        const hasExplicitLabel = !!document.querySelector(`label[for="${CSS.escape(field.id)}"]`);
        const hasAriaLabel = field.hasAttribute('aria-label') || field.hasAttribute('aria-labelledby');
        const isInsideLabel = !!field.closest('label');

        if (!hasExplicitLabel && !hasAriaLabel && !isInsideLabel) {
            field.setAttribute('aria-label', readableLabel);
        }
    };

    const patchAllFields = () => {
        document.querySelectorAll<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>(fieldSelector)
            .forEach(patchField);
    };

    patchAllFields();

    const observer = new MutationObserver((mutations) => {
        if (mutations.some(mutation => mutation.addedNodes.length > 0)) {
            patchAllFields();
        }
    });

    observer.observe(document.body, { childList: true, subtree: true });
};

platformBrowserDynamic().bootstrapModule(AppModule)
    .then(() => ensureAccessibleFormFields())
    .catch(err => console.error(err));
