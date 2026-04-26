import { Injectable } from '@angular/core';
import { ToastrService } from 'ngx-toastr';

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  constructor(private toastr: ToastrService) {}

  private playSound(type: 'success' | 'error' | 'warning' | 'info') {
    try {
      let soundPath = 'assets/sounds/new-message-sound-in-chat.mp3';
      
      if (type === 'success') {
        soundPath = 'assets/sounds/snapchat-sound.mp3';
      } else if (type === 'error' || type === 'warning') {
        soundPath = 'assets/sounds/discord-join.mp3';
      }
      
      const audio = new Audio(soundPath);
      audio.volume = 0.5;
      audio.play().catch(err => console.warn('Could not play notification sound:', err));
    } catch (e) {
      console.warn('Audio playback not supported');
    }
  }

  success(message: string, title: string = 'تم بنجاح'): void {
    this.toastr.success(message, title);
    this.playSound('success');
  }

  error(message: string, title: string = 'حدث خطأ'): void {
    this.toastr.error(message, title);
    this.playSound('error');
  }

  warning(message: string, title: string = 'تنبيه'): void {
    this.toastr.warning(message, title);
    this.playSound('warning');
  }

  info(message: string, title: string = 'معلومة'): void {
    this.toastr.info(message, title);
    this.playSound('info');
  }
}
