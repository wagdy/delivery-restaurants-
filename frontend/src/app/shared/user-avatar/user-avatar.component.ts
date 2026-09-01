import { Component, computed, input } from '@angular/core';

// Curated gradient pairs so avatars stay visually pleasant no matter which
// name hashes to them, rather than generating arbitrary (and sometimes ugly) colors.
const GRADIENTS: [string, string][] = [
  ['#667eea', '#764ba2'],
  ['#f093fb', '#f5576c'],
  ['#4facfe', '#00f2fe'],
  ['#43e97b', '#0ba360'],
  ['#fa709a', '#fee140'],
  ['#30cfd0', '#330867'],
  ['#ff9a9e', '#c471ed'],
  ['#0ba29e', '#2b5876']
];

@Component({
  selector: 'app-user-avatar',
  standalone: true,
  template: `
    <div class="avatar" [style.width.px]="size()" [style.height.px]="size()" [style.background]="gradient()" [style.font-size.px]="size() * 0.4">
      {{ initials() }}
    </div>
  `,
  styles: [
    `
      .avatar {
        border-radius: 50%;
        display: flex;
        align-items: center;
        justify-content: center;
        color: #fff;
        font-weight: 600;
        letter-spacing: 0.02em;
        box-shadow: 0 0 0 2px rgba(255, 255, 255, 0.7);
        flex-shrink: 0;
        user-select: none;
      }
    `
  ]
})
export class UserAvatarComponent {
  readonly fullName = input.required<string>();
  readonly size = input<number>(36);

  readonly initials = computed(() => {
    const parts = this.fullName().trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) {
      return '?';
    }
    if (parts.length === 1) {
      return parts[0].charAt(0).toUpperCase();
    }
    return (parts[0].charAt(0) + parts[parts.length - 1].charAt(0)).toUpperCase();
  });

  readonly gradient = computed(() => {
    const [from, to] = GRADIENTS[this.hashName() % GRADIENTS.length];
    return `linear-gradient(135deg, ${from}, ${to})`;
  });

  private hashName(): number {
    const name = this.fullName();
    let hash = 0;
    for (let i = 0; i < name.length; i++) {
      hash = (hash << 5) - hash + name.charCodeAt(i);
      hash |= 0;
    }
    return Math.abs(hash);
  }
}
