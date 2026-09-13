import { Directive, ElementRef, HostListener, inject, input } from '@angular/core';

// Pointer-tracked 3D tilt + light-sheen highlight for "premium card" surfaces. Writes
// only CSS custom properties (--tilt-rx/--tilt-ry/--tilt-px/--tilt-py) onto the host -
// the host's own stylesheet decides what to do with them (transform, gradient position,
// or nothing), so this directive stays reusable across every card in the app rather than
// baking in one specific look.
//
// Mouse-only by design (touch devices get the plain, untilted card - a finger on the
// glass can't "hover" to preview the effect anyway) and fully inert under
// prefers-reduced-motion.
@Directive({
  selector: '[appTilt]',
  standalone: true
})
export class TiltDirective {
  private readonly el = inject(ElementRef<HTMLElement>);
  readonly appTiltMax = input(10, { alias: 'appTilt', transform: (v: unknown) => (v === '' || v == null ? 10 : Number(v)) });

  private readonly reducedMotion =
    typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;

  @HostListener('pointermove', ['$event'])
  onPointerMove(event: PointerEvent): void {
    if (this.reducedMotion || event.pointerType !== 'mouse') {
      return;
    }

    const rect = this.el.nativeElement.getBoundingClientRect();
    const px = (event.clientX - rect.left) / rect.width;
    const py = (event.clientY - rect.top) / rect.height;
    const max = this.appTiltMax();

    const style = this.el.nativeElement.style;
    style.setProperty('--tilt-rx', `${(0.5 - py) * 2 * max}deg`);
    style.setProperty('--tilt-ry', `${(px - 0.5) * 2 * max}deg`);
    style.setProperty('--tilt-px', `${px * 100}%`);
    style.setProperty('--tilt-py', `${py * 100}%`);
  }

  @HostListener('pointerleave')
  onPointerLeave(): void {
    const style = this.el.nativeElement.style;
    style.setProperty('--tilt-rx', '0deg');
    style.setProperty('--tilt-ry', '0deg');
    style.setProperty('--tilt-px', '50%');
    style.setProperty('--tilt-py', '50%');
  }
}
