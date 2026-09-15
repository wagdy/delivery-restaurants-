import { Pipe, PipeTransform } from '@angular/core';
import { Language } from '../../core/services/language.service';

// Anything with an English name and an optional Arabic one: Category, MenuItem, AddOn,
// SubCategory all satisfy this structurally, so the pipe needs no per-type overloads.
export interface LocalizableName {
  name: string;
  nameAr?: string | null;
}

// Returns the Arabic name when Arabic is selected AND one has actually been entered,
// and the English name in every other case.
//
// The language is passed as an ARGUMENT rather than read from LanguageService inside the
// pipe, which is what lets this stay a pure pipe. A pure pipe re-runs only when its
// inputs change by reference; a pipe that read the service internally would not re-run on
// a language switch at all unless it were marked pure: false, which would re-run it on
// every change-detection cycle for every name on the menu. Passing the language makes the
// switch itself the input change - correct AND cheap. Usage:
//
//   {{ item | localName: language() }}
@Pipe({ name: 'localName', standalone: true })
export class LocalNamePipe implements PipeTransform {
  transform(value: LocalizableName | null | undefined, language: Language): string {
    if (!value) {
      return '';
    }

    // The whitespace check is the whole point of this pipe. An Arabic name that is
    // present but blank - an admin who typed a space, or cleared the field in a browser
    // that posts "" rather than omitting it - must fall back to English, not render a
    // nameless dish. The backend normalises blanks to null on write (see
    // OptionalText.NullIfBlank), so this is the second line of defence, covering rows
    // written before that existed.
    if (language === 'ar' && value.nameAr?.trim()) {
      return value.nameAr.trim();
    }

    return value.name;
  }
}
