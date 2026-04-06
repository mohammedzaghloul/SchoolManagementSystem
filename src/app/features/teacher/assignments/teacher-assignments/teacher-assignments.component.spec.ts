import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TeacherAssignmentsComponent } from './teacher-assignments.component';

describe('TeacherAssignmentsComponent', () => {
  let component: TeacherAssignmentsComponent;
  let fixture: ComponentFixture<TeacherAssignmentsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TeacherAssignmentsComponent]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(TeacherAssignmentsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
