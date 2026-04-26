import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { Subscription } from 'rxjs';

import { Video } from '../../../core/models/video.model';
import { ApiService } from '../../../core/services/api.service';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { SearchService } from '../../../core/services/search.service';
import { SubjectService } from '../../../core/services/subject.service';
import { VideoService } from '../../../core/services/video.service';

@Component({
  selector: 'app-videos',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './videos.component.html',
  styleUrls: ['./videos.component.css']
})
export class VideosComponent implements OnInit, OnDestroy {
  showModal = false;
  loading = false;
  isEditing = false;
  currentUserRole = '';
  videos: Video[] = [];
  filteredVideos: Video[] = [];
  subjects: any[] = [];
  grades: any[] = [];
  searchSub?: Subscription;

  showPlayer = false;
  selectedVideoUrl: SafeResourceUrl | null = null;
  activeVideo: Video | null = null;

  showSuccessOverlay = false;
  successOverlayTitle = 'تم الحفظ بنجاح!';
  successOverlayMessage = 'تم تحديث المحتوى المرئي في قاعدة البيانات بنجاح.';

  showDeleteOverlay = false;
  pendingDeleteVideo: Video | null = null;
  deletingVideo = false;

  newVideo: any = this.getEmptyVideo();

  private successOverlayTimer: ReturnType<typeof setTimeout> | null = null;

  constructor(
    private videoService: VideoService,
    private subjectService: SubjectService,
    private searchService: SearchService,
    private sanitizer: DomSanitizer,
    private api: ApiService,
    private auth: AuthService,
    private notificationService: NotificationService
  ) {}

  ngOnInit(): void {
    const user = this.auth.getCurrentUser();
    this.currentUserRole = user?.role || '';

    this.loadVideos();
    this.loadDependencies();

    this.searchSub = this.searchService.searchTerm$.subscribe(term => {
      this.filterVideos(term);
    });
  }

  ngOnDestroy(): void {
    this.searchSub?.unsubscribe();

    if (this.successOverlayTimer) {
      clearTimeout(this.successOverlayTimer);
      this.successOverlayTimer = null;
    }
  }

  filterVideos(term: string): void {
    const normalizedTerm = (term || '').trim().toLowerCase();
    if (!normalizedTerm) {
      this.filteredVideos = [...this.videos];
      return;
    }

    this.filteredVideos = this.videos.filter(video =>
      video.title?.toLowerCase().includes(normalizedTerm) ||
      video.subject?.toLowerCase().includes(normalizedTerm) ||
      video.description?.toLowerCase().includes(normalizedTerm) ||
      video.gradeName?.toLowerCase().includes(normalizedTerm)
    );
  }

  async loadDependencies(): Promise<void> {
    try {
      const [subjects, grades] = await Promise.all([
        this.subjectService.getAll(),
        this.api.get<any[]>('/api/Videos/grades')
      ]);

      this.subjects = subjects;
      this.grades = grades;
    } catch (error) {
      console.error('Failed to load video dependencies', error);
    }
  }

  async loadVideos(): Promise<void> {
    this.loading = true;

    try {
      this.videos = await this.videoService.getVideos();
      this.filteredVideos = [...this.videos];
    } catch (error) {
      console.error('Failed to load videos', error);
      this.videos = [];
      this.filteredVideos = [];
      this.notificationService.error('تعذر تحميل قائمة الفيديوهات الآن.');
    } finally {
      this.loading = false;
    }
  }

  openModal(): void {
    this.isEditing = false;
    this.newVideo = this.getEmptyVideo();
    this.showModal = true;
  }

  closeModal(): void {
    this.showModal = false;
    this.newVideo = this.getEmptyVideo();
  }

  async publishVideo(): Promise<void> {
    if (!this.newVideo.url || !this.newVideo.title || !this.newVideo.subjectId) {
      this.notificationService.warning('يرجى ملء رابط الفيديو والعنوان والمادة قبل الحفظ.');
      return;
    }

    const youtubeId = this.extractYouTubeId(this.newVideo.url);
    if (youtubeId) {
      this.newVideo.thumbnailUrl = `https://img.youtube.com/vi/${youtubeId}/hqdefault.jpg`;
    }

    const payload: any = {
      title: this.newVideo.title,
      description: this.newVideo.description,
      url: this.newVideo.url,
      thumbnailUrl: this.newVideo.thumbnailUrl,
      duration: this.newVideo.duration,
      subjectId: this.newVideo.subjectId,
      gradeLevelId: this.newVideo.gradeLevelId,
      isHidden: this.newVideo.isHidden
    };

    try {
      if (this.isEditing) {
        payload.id = this.newVideo.id;
        await this.api.put(`/api/Videos/${this.newVideo.id}`, payload);
      } else {
        await this.api.post('/api/Videos', payload);
      }

      await this.loadVideos();
      this.closeModal();
      this.flashSuccessOverlay(
        'تم الحفظ بنجاح!',
        this.isEditing
          ? 'تم تحديث بيانات الفيديو في قاعدة البيانات.'
          : 'تم إضافة الفيديو إلى المكتبة التعليمية بنجاح.'
      );
    } catch (error) {
      console.error('Failed to save video', error);
      this.notificationService.error('تعذر حفظ الفيديو الآن. حاول مرة أخرى.');
    }
  }

  editVideo(video: any): void {
    this.isEditing = true;
    this.newVideo = {
      id: video.id,
      url: video.url,
      title: video.title,
      description: video.description,
      subjectId: video.subjectId || null,
      gradeLevelId: video.gradeLevelId || null,
      thumbnailUrl: video.thumbnailUrl || video.thumbnail,
      duration: video.duration || '10:00',
      isHidden: !!video.isHidden
    };
    this.showModal = true;
  }

  async playVideo(video: Video): Promise<void> {
    this.incrementVideoViews(video);

    const youtubeId = this.extractYouTubeId(video.url);

    if (youtubeId) {
      this.selectedVideoUrl = this.sanitizer.bypassSecurityTrustResourceUrl(
        `https://www.youtube.com/embed/${youtubeId}?autoplay=1`
      );
      this.activeVideo = video;
      this.showPlayer = true;
      return;
    }

    window.open(video.url, '_blank');
  }

  private async incrementVideoViews(video: Video): Promise<void> {
    try {
      const result = await this.videoService.incrementViews(video.id);
      video.views = result.views;
    } catch {
      video.views = (video.views || 0) + 1;
    }
  }

  async toggleLock(video: any): Promise<void> {
    const previousState = !!video.isHidden;
    video.isHidden = !previousState;

    const payload = {
      id: video.id,
      title: video.title,
      description: video.description,
      url: video.url,
      thumbnailUrl: video.thumbnailUrl || video.thumbnail,
      subjectId: video.subjectId,
      gradeLevelId: video.gradeLevelId,
      isHidden: video.isHidden
    };

    try {
      await this.api.put(`/api/Videos/${video.id}`, payload);
      this.flashSuccessOverlay(
        'تم التحديث بنجاح!',
        video.isHidden ? 'تم إخفاء الفيديو عن الطلاب.' : 'تم إظهار الفيديو للطلاب.'
      );
    } catch (error) {
      video.isHidden = previousState;
      console.error('Failed to toggle video visibility', error);
      this.notificationService.error('تعذر تحديث حالة الفيديو الآن.');
    }
  }

  deleteVideo(video: Video): void {
    this.pendingDeleteVideo = video;
    this.showDeleteOverlay = true;
  }

  cancelDeleteVideo(): void {
    if (this.deletingVideo) {
      return;
    }

    this.pendingDeleteVideo = null;
    this.showDeleteOverlay = false;
  }

  async confirmDeleteVideo(): Promise<void> {
    if (!this.pendingDeleteVideo || this.deletingVideo) {
      return;
    }

    const video = this.pendingDeleteVideo;
    this.deletingVideo = true;

    try {
      await this.api.delete(`/api/Videos/${video.id}`);
      this.videos = this.videos.filter(item => item.id !== video.id);
      this.filteredVideos = this.filteredVideos.filter(item => item.id !== video.id);
      this.pendingDeleteVideo = null;
      this.showDeleteOverlay = false;
      this.flashSuccessOverlay('تم الحذف بنجاح!', 'تم حذف الفيديو من المكتبة التعليمية بنجاح.');
    } catch (error) {
      console.error('Failed to delete video', error);
      this.notificationService.error('تعذر حذف الفيديو الآن. حاول مرة أخرى.');
    } finally {
      this.deletingVideo = false;
    }
  }

  private getEmptyVideo(): any {
    return {
      id: null,
      url: '',
      title: '',
      description: '',
      subjectId: null,
      gradeLevelId: null,
      thumbnailUrl:
        'https://images.unsplash.com/photo-1503676260728-1c00da094a0b?auto=format&fit=crop&q=80&w=400&h=250',
      duration: '10:00',
      isHidden: false
    };
  }

  private extractYouTubeId(url: string): string | null {
    if (!url) {
      return null;
    }

    const regExp = /^.*((youtu.be\/)|(v\/)|(\/u\/\w\/)|(embed\/)|(watch\?v=)|(\?v=)|(shorts\/)|(live\/))([^#&?]*).*/;
    const match = url.match(regExp);
    return match && match[10]?.length === 11 ? match[10] : null;
  }

  private flashSuccessOverlay(title: string, message: string): void {
    if (this.successOverlayTimer) {
      clearTimeout(this.successOverlayTimer);
    }

    this.successOverlayTitle = title;
    this.successOverlayMessage = message;
    this.showSuccessOverlay = true;

    this.successOverlayTimer = setTimeout(() => {
      this.showSuccessOverlay = false;
      this.successOverlayTimer = null;
    }, 2600);
  }
}
