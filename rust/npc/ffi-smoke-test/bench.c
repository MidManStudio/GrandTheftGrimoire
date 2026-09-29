#define _POSIX_C_SOURCE 200809L
#include "gtg_npc.h"
#include <inttypes.h>
#include <stdio.h>
#include <stdlib.h>
#include <time.h>
static double seconds(void) { struct timespec t; clock_gettime(CLOCK_MONOTONIC,&t); return t.tv_sec+t.tv_nsec*1e-9; }
int main(void) {
 const unsigned counts[]={10,100,500,1000};
 puts("batch,iterations,elapsed_s,ns_per_npc,npc_per_s,ns_per_call");
 for(unsigned j=0;j<4;j++) {
  unsigned n=counts[j], iters=1000000u/n; if(iters<1000) iters=1000;
  GtgNpcObservation *in=calloc(n,sizeof(*in)); GtgNpcDecision *out=calloc(n,sizeof(*out));
  if(!in||!out) return 2;
  for(unsigned i=0;i<n;i++) { in[i].npc_id=i+1; in[i].role=3; in[i].backend=1; in[i].threat_visible=1; in[i].health_fraction=.8f; in[i].can_move=1; }
  for(unsigned w=0;w<100;w++) if(gtg_npc_decide_batch(in,n,out,n)) return 3;
  double start=seconds(); uint64_t checksum=0;
  for(unsigned k=0;k<iters;k++) { if(gtg_npc_decide_batch(in,n,out,n)) return 4; checksum+=(unsigned)out[k%n].action; }
  double elapsed=seconds()-start; if(!checksum||elapsed<=0) return 5;
  printf("%u,%u,%.9f,%.2f,%.0f,%.2f\n",n,iters,elapsed,elapsed*1e9/(n*(double)iters),n*(double)iters/elapsed,elapsed*1e9/iters);
  free(in);free(out);
 }
 return 0;
}
