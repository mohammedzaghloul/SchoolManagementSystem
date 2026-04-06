import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { Video } from '../../../core/models/video.model';
import { VideoService } from '../../../core/services/video.service';
import { SubjectService } from '../../../core/services/subject.service';
import { SearchService } from '../../../core/services/search.service';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { AuthService } from '../../../core/services/auth.service';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-videos',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './videos.component.html',
  styleUrls: ['./videos.component.css']
})
export class VideosComponent implements OnInit {
  showModal = false;
  loading = false;
  isEditing = false;
  currentUserRole = '';
  videos: Video[] = [];
  filteredVideos: any[] = [];
  subjects: any[] = [];
  grades: any[] = [];
  searchSub!: Subscription;

  // For Playing Video
  showPlayer = false;
  selectedVideoUrl: SafeResourceUrl | null = null;
  activeVideo: Video | null = null;

  newVideo: any = {
    id: null,
    url: '',
    title: '',
    description: '',
    subjectId: null,
    gradeLevelId: null,
    thumbnailUrl: 'https://images.unsplash.com/photo-1503676260728-1c00da094a0b?auto=format&fit=crop&q=80&w=400&h=250',
    duration: '10:00',
    isHidden: false
  };

  constructor(
    private videoService: VideoService,
    private subjectService: SubjectService,
    private searchService: SearchService,
    private sanitizer: DomSanitizer,
    private api: ApiService,
    private auth: AuthService
  ) { }

  ngOnInit() {
    const user = this.auth.getCurrentUser();
    this.currentUserRole = user?.role || '';
    
    this.loadVideos();
    this.loadDependencies();

    this.searchSub = this.searchService.searchTerm$.subscribe(term => {
      this.filterVideos(term);
    });
  }

  ngOnDestroy() {
    if (this.searchSub) this.searchSub.unsubscribe();
  }

  filterVideos(term: string) {
    if (!term) {
      this.filteredVideos = [...this.videos];
      return;
    }
    const t = term.toLowerCase();
    this.filteredVideos = this.videos.filter(v =>
      v.title.toLowerCase().includes(t) ||
      (v.subject && v.subject.toLowerCase().includes(t)) ||
      (v.description && v.description.toLowerCase().includes(t)) ||
      (v.gradeName && v.gradeName.toLowerCase().includes(t))
    );
  }

  async loadDependencies() {
    try {
      const [subs, grades] = await Promise.all([
        this.subjectService.getAll(),
        this.api.get<any[]>('/api/Videos/grades')
      ]);
      this.subjects = subs;
      this.grades = grades;
    } catch (err) {
      console.error('Failed to load dependencies', err);
    }
  }

  async loadVideos() {
    this.loading = true;
    try {
      this.videos = await this.videoService.getVideos();
      this.filteredVideos = [...this.videos];
    } catch (err) {
      console.error('Failed to load videos', err);
      this.videos = [];
      this.filteredVideos = [];
    } finally {
      this.loading = false;
    }
  }

  openModal() {
    this.isEditing = false;
    this.resetForm();
    this.showModal = true;
  }

  closeModal() {
    this.showModal = false;
    this.resetForm();
  }

  private resetForm() {
    this.newVideo = {
      id: null,
      url: '',
      title: '',
      description: '',
      subjectId: null,
      thumbnailUrl: 'https://images.unsplash.com/photo-1503676260728-1c00da094a0b?auto=format&fit=crop&q=80&w=400&h=250',
      duration: '10:00',
      isHidden: false
    };
  }

  async publishVideo() {
    if (!this.newVideo.url || !this.newVideo.title || !this.newVideo.subjectId) {
      alert('يرجى ملء جميع الحقول المطلوبة');
      return;
    }

    // Extraction logic
    const youtubeRegex = /(?:https?:\/\/)?(?:www\.)?(?:youtube\.com\/(?:[^\/\n\s]+\/\S+\/|(?:v|e(?:mbed)?)\/|\S*?[?&]v=)|youtu\.be\/)([a-zA-Z0-9_-]{11})/;
    const match = this.newVideo.url.match(youtubeRegex);
    if (match && match[1]) {
      this.newVideo.thumbnailUrl = `https://img.youtube.com/vi/${match[1]}/hqdefault.jpg`;
    }

    try {
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

      if (this.isEditing) {
        payload.id = this.newVideo.id;
        await this.api.put(`/api/Videos/${this.newVideo.id}`, payload);
      } else {
        await this.api.post('/api/Videos', payload);
      }
      await this.loadVideos();
      this.closeModal();
    } catch (err) {
      console.error('Failed to save video', err);
    }
  }

  editVideo(video: any) {
    this.isEditing = true;
    this.newVideo = {
      id: video.id,
      url: video.url,
      title: video.title,
      description: video.description,
      subjectId: video.subjectId || null,
      gradeLevelId: video.gradeLevelId || null,
      thumbnailUrl: video.thumbnailUrl || video.thumbnail,
      duration: video.duration,
      isHidden: video.isHidden || false
    };
    this.showModal = true;
  }

  playVideo(video: Video) {
    const youtubeId = this.extractYouTubeId(video.url);
    if (youtubeId) {
      this.selectedVideoUrl = this.sanitizer.bypassSecurityTrustResourceUrl(`https://www.youtube.com/embed/${youtubeId}?autoplay=1`);
      this.activeVideo = video;
      this.showPlayer = true;
    } else {
      // For non-YouTube or invalid IDs, open in a new tab to avoid frame errors
      window.open(video.url, '_blank');
    }
  }

  private extractYouTubeId(url: string): string | null {
    if (!url) return null;
    const regExp = /^.*((youtu.be\/)|(v\/)|(\/u\/\w\/)|(embed\/)|(watch\?v=)|(\?v=)|(shorts\/)|(live\/))([^#\&\?]*).*/;
    const match = url.match(regExp);
    return (match && (match[10].length === 11)) ? match[10] : null;
  }

  async toggleLock(video: any) {
    try {
      const originalState = video.isHidden;
      video.isHidden = !video.isHidden;
      
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

      await this.api.put(`/api/Videos/${video.id}`, payload);
    } catch (err) {
      video.isHidden = !video.isHidden; // Revert on fail
      console.error('Failed to toggle lock', err);
    }
  }

  async deleteVideo(video: Video) {
    if (!confirm('هل أنت متأكد من حذف هذا الفيديو؟')) return;
    try {
      await this.api.delete(`/api/Videos/${video.id}`);
      this.videos = this.videos.filter(v => v.id !== video.id);
    } catch (err) {
      console.error('Failed to delete video', err);
    }
  }
}
