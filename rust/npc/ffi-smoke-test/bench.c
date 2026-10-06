// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/gtg-npc-ffi.md, section "bench.c"
// ============================================================================
#define _POSIX_C_SOURCE 200809L
/* C-to-Rust FFI benchmark of gtg_npc_decide_batch. Mirrors crates/npc-ffi/examples/decision_throughput.rs:
   same two scenarios, same generator, same window sliding, same repetition scheme (REPS timed repetitions after
   a warm-up, median reported with min and max). Keep the two in sync. */
#include "gtg_npc.h"
#include <inttypes.h>
#include <stdio.h>
#include <stdlib.h>
#include <time.h>
#define POOL 16384u
#define SEED 0x47544700ull
#define SEED2 0x47544701ull
#define ACTIONS 8
#define WINDOW_STEP 977u
#define REPS 9
#define NPC_DECISIONS_PER_REP 4000000u
static double seconds(void) { struct timespec t; clock_gettime(CLOCK_MONOTONIC,&t); return t.tv_sec+t.tv_nsec*1e-9; }
static uint64_t mix(uint64_t x) {
 x += 0x9E3779B97F4A7C15ull;
 uint64_t z = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9ull;
 z = (z ^ (z >> 27)) * 0x94D049BB133111EBull;
 return z ^ (z >> 31);
}
static void fill_uniform(GtgNpcObservation *p) {
 for (unsigned i=0;i<POOL;i++) { p[i]=(GtgNpcObservation){0}; p[i].npc_id=i+1; p[i].role=3; p[i].backend=1; p[i].threat_visible=1; p[i].health_fraction=.8f; p[i].can_move=1; }
}
static void fill_mixed(GtgNpcObservation *p) {
 for (unsigned i=0;i<POOL;i++) {
  uint64_t r = mix(SEED + i), r2 = mix(SEED2 + i), pct = r % 100;
  uint32_t role = pct<12 ? 0 : pct<50 ? 1 : pct<62 ? 2 : pct<82 ? 3 : pct<90 ? 4 : 5;
  uint32_t sel = (uint32_t)((r >> 8) % 2);
  p[i]=(GtgNpcObservation){0};
  p[i].npc_id = i+1; p[i].role = role;
  p[i].backend = role<=1 ? 0 : role==2 ? 1 : (role==3 || role==5) ? sel : 1+sel;
  p[i].threat_visible = ((r >> 16) % 100) < 25;
  p[i].health_fraction = (float)((r >> 24) % 1001) / 1000.0f;
  p[i].can_move = (role==0 || ((r >> 40) % 100) < 10) ? 0 : 1;
  uint64_t d = r2 % 100;
  p[i].disposition = (role>=2 && role<=4) ? (d<60 ? 0 : d<85 ? 1 : 2) : 0;
  p[i].provoked = ((r2 >> 8) % 100) < 20;
  p[i].level = (uint16_t)(1 + (r2 >> 16) % 30);
  p[i].order = role==5 ? (uint8_t)((r2 >> 24) % 4) : 0;
  p[i].order_level = role==5 ? (uint16_t)((r2 >> 32) % 40) : 0;
 }
}
static int verify(const GtgNpcObservation *pool) {
 uint64_t hist[ACTIONS]={0}, hash=0xcbf29ce484222325ull; GtgNpcDecision out[1024];
 for (unsigned off=0; off<POOL; off+=1024) {
  if (gtg_npc_decide_batch(pool+off,1024,out,1024)) return 6;
  for (unsigned i=0;i<1024;i++) { if (out[i].action<0||out[i].action>=ACTIONS) return 7; hist[out[i].action]++; hash=(hash ^ ((uint64_t)out[i].action+1)) * 0x100000001b3ull; }
 }
 for (int a=0;a<ACTIONS;a++) if (!hist[a]) return 8;
 printf("# mixed_actions idle=%" PRIu64 " trade=%" PRIu64 " patrol=%" PRIu64 " attack=%" PRIu64 " retreat=%" PRIu64 " follow=%" PRIu64 " hold=%" PRIu64 " refuse=%" PRIu64 " fnv1a=0x%016" PRIx64 "\n",hist[0],hist[1],hist[2],hist[3],hist[4],hist[5],hist[6],hist[7],hash);
 return 0;
}
static volatile uint64_t sink;
static int cmp_double(const void *a, const void *b) { double x=*(const double*)a, y=*(const double*)b; return (x>y)-(x<y); }
static int bench(const char *scenario, const GtgNpcObservation *pool, unsigned n, unsigned step) {
 unsigned iters=NPC_DECISIONS_PER_REP/n; if(iters<1000) iters=1000;
 unsigned span=POOL-n, off=0, warm=iters/10; if(warm<100) warm=100;
 GtgNpcDecision *out=calloc(n,sizeof(*out)); if(!out) return 2;
 for(unsigned w=0;w<warm;w++) { if(gtg_npc_decide_batch(pool+off,n,out,n)) return 3; off+=step; if(off>=span) off-=span; }
 double ns[REPS], total=0; uint64_t acc=0;
 for(int r=0;r<REPS;r++) {
  double start=seconds();
  for(unsigned k=0;k<iters;k++) { if(gtg_npc_decide_batch(pool+off,n,out,n)) return 4; acc+=(unsigned)out[0].action; off+=step; if(off>=span) off-=span; }
  double elapsed=seconds()-start; if(elapsed<=0) return 5;
  total+=elapsed; ns[r]=elapsed*1e9/(n*(double)iters);
 }
 sink=acc;
 qsort(ns,REPS,sizeof(double),cmp_double);
 double med=ns[REPS/2];
 printf("%s,%u,%u,%d,%.9f,%.2f,%.2f,%.2f,%.0f,%.2f\n",scenario,n,iters,REPS,total,med,ns[0],ns[REPS-1],1e9/med,med*n);
 free(out); return 0;
}
int main(void) {
 const unsigned counts[]={10,100,500,1000};
 GtgNpcObservation *uni=malloc(POOL*sizeof(*uni)), *mixed=malloc(POOL*sizeof(*mixed));
 if(!uni||!mixed) return 2;
 fill_uniform(uni); fill_mixed(mixed);
 puts("scenario,batch,iterations,reps,elapsed_s,ns_per_npc,min_ns_per_npc,max_ns_per_npc,npc_per_s,ns_per_call");
 int rc;
 for(unsigned j=0;j<4;j++) if((rc=bench("uniform",uni,counts[j],0))) return rc;
 for(unsigned j=0;j<4;j++) if((rc=bench("mixed",mixed,counts[j],WINDOW_STEP))) return rc;
 if((rc=verify(mixed))) return rc;
 free(uni); free(mixed);
 return 0;
}
